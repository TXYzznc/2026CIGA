"""Codex CLI 调用封装。

核心约束（实测达 ~93% token 节省）：
- cwd 切到 /tmp/codex-art-gen-runner（避项目本地 SKILL/agent 加载）
- --skip-git-repo-check（绕 trusted dir 检查）
- --ephemeral（不污染 session 历史）
- -s workspace-write + writable_roots 显式授权写出
- -m gpt-5.5（默认模型；调用方可通过 model 参数覆盖为 mini 系列以省信用）
- -o result.json（codex 返回写到文件，外部直接读）
- prompt 从 stdin 传（避复杂引号问题）
"""
from __future__ import annotations

import asyncio
import json
import os
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path
from typing import Any

RUNNER_DIR = Path(tempfile.gettempdir()) / "codex-art-gen-runner"
RUNNER_AGENTS_MD = """你是图片生成执行器。只生成图片并返回 JSON，不解释、不规划、不读取项目。
"""


def find_codex_exe() -> str:
    """Windows 下 codex 通常是 .cmd shim，需要走 cmd.exe 调用。"""
    # 优先环境变量
    if env := os.environ.get("CODEX_EXE"):
        return env
    # shutil.which 能找到 .cmd / .exe
    p = shutil.which("codex")
    if p:
        return p
    # 兜底常见 npm 路径
    candidates = [
        Path(os.environ.get("APPDATA", "")) / "npm" / "codex.cmd",
        Path(os.environ.get("APPDATA", "")) / "npm" / "codex.exe",
        Path.home() / "AppData/Roaming/npm/codex.cmd",
    ]
    for c in candidates:
        if c.exists():
            return str(c)
    raise FileNotFoundError("codex executable not found in PATH; set CODEX_EXE env var")


CODEX_EXE = find_codex_exe()
# Windows .cmd 需要 shell=True 或显式 cmd.exe /c 走
USE_SHELL = sys.platform == "win32" and CODEX_EXE.lower().endswith(".cmd")


def ensure_runner_dir() -> Path:
    """首次启动建 runner cwd 隔离目录。"""
    RUNNER_DIR.mkdir(parents=True, exist_ok=True)
    agents_md = RUNNER_DIR / "AGENTS.md"
    if not agents_md.exists():
        agents_md.write_text(RUNNER_AGENTS_MD, encoding="utf-8")
    return RUNNER_DIR


async def run_codex_exec(
    prompt: str,
    result_json_path: Path,
    writable_roots: list[str],
    model: str = "gpt-5.5",
    log_path: Path | None = None,
    timeout_sec: int = 900,
    images: list[str | Path] | None = None,
) -> dict[str, Any]:
    """单次 codex exec。返回 {success, result, error, token_estimate}。

    Args:
        prompt: 完整 prompt 文本（已含批次 JSON 等）
        result_json_path: codex 输出的 JSON 写到这里（绝对路径）
        writable_roots: sandbox 允许写出的目录绝对路径列表
        model: 默认 gpt-5.5；调用方可显式传 mini 系列省信用
        log_path: stderr+stdout 重定向到此文件（并发不交叠）
        timeout_sec: 单次 codex exec 最长 15 分钟
        images: 可选参考图绝对路径列表（每张走独立 -i，避免逗号路径冲突）
    """
    runner_cwd = ensure_runner_dir()
    # writable_roots 转 TOML 数组字符串（注意 Windows 反斜杠转义）
    roots_toml = "[" + ",".join(
        f'"{Path(r).resolve().as_posix()}"' for r in writable_roots
    ) + "]"

    args = [
        "exec",
        "--skip-git-repo-check",
        "--ephemeral",
        "-s", "workspace-write",
        "-c", f"sandbox_workspace_write.writable_roots={roots_toml}",
        "-m", model,
        "-o", str(result_json_path.resolve()),
    ]
    if images:
        for img in images:
            args.extend(["-i", str(Path(img).resolve())])
    args.append("-")  # prompt 从 stdin 读，必须放在末尾

    log_fh = open(log_path, "w", encoding="utf-8") if log_path else subprocess.DEVNULL
    try:
        # Windows .cmd shim 必须走 cmd.exe；Python 自动用 list2cmdline 转义参数（含 TOML 数组里的双引号）
        if USE_SHELL:
            real_args = ["cmd.exe", "/c", CODEX_EXE, *args]
        else:
            real_args = [CODEX_EXE, *args]
        proc = await asyncio.create_subprocess_exec(
            *real_args,
            stdin=asyncio.subprocess.PIPE,
            stdout=log_fh,
            stderr=asyncio.subprocess.STDOUT,
            cwd=str(runner_cwd),
        )
        try:
            await asyncio.wait_for(
                proc.communicate(input=prompt.encode("utf-8")),
                timeout=timeout_sec,
            )
        except asyncio.TimeoutError:
            proc.kill()
            return {"success": False, "error": f"codex exec timeout {timeout_sec}s"}

        if proc.returncode != 0:
            return {"success": False, "error": f"codex exec exit {proc.returncode}"}

        if not result_json_path.exists():
            return {"success": False, "error": "codex didn't write result json"}

        try:
            result = json.loads(result_json_path.read_text(encoding="utf-8"))
        except json.JSONDecodeError as e:
            return {"success": False, "error": f"result not valid JSON: {e}"}

        return {"success": True, "result": result}
    finally:
        if log_fh != subprocess.DEVNULL:
            log_fh.close()
