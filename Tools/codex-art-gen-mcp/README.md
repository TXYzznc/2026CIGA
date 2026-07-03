# codex-art-gen MCP

> Claude 做脑, Codex 做手。把 codex CLI 生图固化为常驻 MCP 服务,省 ~93% token。
>
> v2 (09-mcp-decouple): MCP 完全无业务,所有项目级展开放在 `tools/codex-art-gen-helper/`。

## 工具 (3 个)

| 工具 | 输入 | 输出 |
|---|---|---|
| `dispatch_l1` | `batches[{batch_id, writable_roots[], items[{index,name,file(abs),size,transparent,prompt,negative}]}]` | 1 batch = 1 codex session 内多次 image_gen |
| `dispatch_l2` | `sheets[{sheet_id, canvas(abs), writable_roots[], grid_rows, grid_cols, chroma_key?, items[{index,name,target_file(abs),prompt,negative}]}]` | 1 sheet = 1 codex session 1 次 image_gen → 本地 chroma_key + image_cut |
| `write_record` | `record_path(abs)`, `results[]` | 追加 markdown 表格 |

可选参数 `concurrency`（默认 2）控制并发跑几路。

## 典型流程

```python
# 调用方（如 Claude 主对话或项目级脚本）展开业务后直传 envelope:
from codex_art_gen_helper.expand_v21 import build_envelopes
l1, l2 = build_envelopes(change_name="06-v21-implementation")

mcp.dispatch_l1(batches=l1)
mcp.dispatch_l2(sheets=l2)
mcp.write_record(record_path=".../生成记录.md", results=[...])
```

## 单次 codex exec 参数（实测 ~3K token/调用)

```
codex exec \
  --skip-git-repo-check \
  --ephemeral \
  -s workspace-write \
  -c 'sandbox_workspace_write.writable_roots=["<writable_roots[0] 绝对路径>"]' \
  -m gpt-5.5 \
  -o <result.json> \
  -  # prompt 从 stdin
```

cwd 强制 `<TEMP>/codex-art-gen-runner/`（避免加载项目 SKILL/agent）。

## 边界

- 不读 prompts.md / 项目文件;调用方负责展开
- 不假设 REPO_ROOT 位置;所有路径必须绝对
- 不带风格指南;美术风格写在每个 item.prompt 里
- 不支持 codex 内重试(每 item 最多 1 次 image_gen)
- 失败 item 标 `failed` 继续,不抛异常

## 外部工具路径（跨项目可 env 覆盖)

| Env 变量 | 默认（本仓库 layout)|
|---|---|
| `CODEX_ART_GEN_IMAGE_CUT_PY` | `<repo>/tools/ImageCut_Tool/image_cut.py` |
| `CODEX_ART_GEN_CHROMA_KEY_PY` | `<repo>/tools/chroma_key_tool/chroma_key.py` |
| `CODEX_ART_GEN_PYTHON` | `<repo>/.venv/Scripts/python.exe` |
| `CODEX_EXE` | `shutil.which('codex')` 或 `%APPDATA%/npm/codex.cmd` |

## 注册

`.mcp.json`:
```json
"codex-art-gen": {
  "command": ".venv/Scripts/python.exe",
  "args": ["tools/codex-art-gen-mcp/server.py"]
}
```

`.claude/settings.local.json` `enabledMcpjsonServers` 加 `"codex-art-gen"`。

## 设计依据

- `openspec/changes/08-codex-art-gen-mcp/`：初版 MCP
- `openspec/changes/09-mcp-decouple/`：本次业务解耦（C 方案)
- `openspec/changes/06-v21-implementation/art/codex_input_template_L1_BATCH.md` + `L2_SHEET.md`：Codex 官方推荐输入格式
