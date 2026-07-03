# 环境搭建

新机器拿到项目后，按需执行下面步骤。

## 基础依赖

| 依赖 | 检查命令 | 用途 |
|---|---|---|
| Unity 6.3 LTS | Unity Hub | 打开项目 |
| Python 3.10+ | `python --version` | 可选 MCP / 脚本工具 |
| Node.js 18+ | `node --version` | 可选 MCP / Web 工具 |
| git | `git --version` | 拉取项目与外部工具 |

## Python 环境

```powershell
python -m venv .venv
.venv\Scripts\pip install -r requirements.txt
Copy-Item .env.example .env
```

只在需要 Python MCP 或辅助脚本时执行。`.venv/` 是本机环境，不应提交。

## MCP 配置

`.mcp.json` 保留少量常用 MCP 入口。需要凭据时复制 `.env.example` 为 `.env` 后填写。

未使用的 MCP 或大型工具建议按需安装，不作为模板默认负担。

## 不应同步的目录

这些目录和文件都是可重建产物，应排除在版本控制或模板分发之外：

```text
Library/
Temp/
UserSettings/
logs/
输出日志/
.codebase-memory/
.venv/
*.csproj
*.sln
__pycache__/
node_modules/
.next/
build/
dist/
```
