# unity-skills-routing Specification

## Purpose
TBD - created by archiving change 24-unity-skills-port-routing. Update Purpose after archive.
## Requirements
### Requirement: 客户端寻址优先级链

`.claude/skills/unity-skills/scripts/unity_skills.py` 的 `_default_client` 全局单例 MUST 按以下优先级解析目标端口：

1. **CLI `--port=<num>`**（显式端口，最高优先级）
2. **CLI `--target=<name-or-id>`**（按 registry 反查）
3. **`UNITY_SKILLS_TARGET` 环境变量**（按 registry 反查）
4. **cwd 匹配**：当前工作目录路径规范化后，与 `registry.json` 中各条目的 `path` 字段用 `os.path.commonpath` 判定「cwd 是 path 本身或子目录」，命中即取 `port`
5. **`DEFAULT_PORT = 8090` 兜底**（同时在 stderr 打 warning）

#### Scenario: cwd 命中匹配

- **WHEN** 从项目目录（例如 `D:\unity\UnityProject\GameDesinger` 或其任意子目录）跑 `python unity_skills.py health`
- **THEN** `_default_client.url` 等于 registry 中该项目 `port` 对应的 `http://localhost:<port>`
- **AND** `health()` 返回的 `source` 字段为 `cwd-match(<project-name>)`
- **AND** 不打 stderr warning

#### Scenario: cwd 未命中兜底

- **WHEN** 从任意非 Unity 项目目录（例如 `C:\` 或用户临时目录）跑 `python unity_skills.py health`
- **THEN** `_default_client.url` 等于 `http://localhost:8090`
- **AND** `health()` 返回的 `source` 字段为 `default`
- **AND** stderr 打印一行 warning，提示"cwd 未在 registry 匹配到 Unity 项目，回退 DEFAULT_PORT"

#### Scenario: 显式覆盖生效

- **WHEN** 从任意目录跑 `python unity_skills.py health --target=GameDesinger`
- **THEN** `_default_client.url` 命中 registry 里 `name` 或 `id` 等于 `GameDesinger` 的条目的 `port`
- **AND** `health()` 返回的 `source` 字段为 `cli-target(GameDesinger)`
- **AND** 不打 stderr warning

#### Scenario: 环境变量覆盖

- **WHEN** 设置 `UNITY_SKILLS_TARGET=GameDesinger` 并从任意目录跑 `python unity_skills.py health`
- **THEN** `_default_client.url` 命中该项目 port
- **AND** `source` 为 `env(GameDesinger)`

### Requirement: 双项目并行不串

多个 Unity 项目同时开启服务器时，每个项目的 Claude 会话（cwd 落在各自项目目录下）MUST 只调用到自己项目的 Unity Editor，SHALL NOT 串到其它项目。

#### Scenario: 两 Claude 会话分别在两项目根目录跑同一 skill

- **WHEN** 用户开 `ProjectA`（port 8090）和 `ProjectB`（port 8091），两个 Claude 会话分别在 `ProjectA` 和 `ProjectB` 根目录调 `editor_get_selection`
- **THEN** `ProjectA` 会话的调用打到 `localhost:8090`
- **AND** `ProjectB` 会话的调用打到 `localhost:8091`
- **AND** 两个会话互不干扰

### Requirement: CLI 参数契约

CLI `main()` MUST 在 skill_name 解析之前 pre-parse 以下参数并从 `sys.argv` 中剔除：

- `--target=<name-or-id>` 或 `--target <name-or-id>`
- `--port=<num>` 或 `--port <num>`

剔除后，后续参数解析（`--list` / `--list-instances` / `--stdin-json` / `key=value`）不受影响。

#### Scenario: --port 与 --stdin-json 混用

- **WHEN** 跑 `echo '{"name":"T"}' | python unity_skills.py ui_set_text --port=8091 --stdin-json`
- **THEN** 请求打到 `localhost:8091`
- **AND** 参数从 stdin 读取

#### Scenario: --target 与 key=value 混用

- **WHEN** 跑 `python unity_skills.py gameobject_create --target=GameDesinger name=Cube`
- **THEN** 请求打到 `GameDesinger` 项目的 port
- **AND** `name=Cube` 作为 skill 参数传递

### Requirement: health() 返回契约

`health()` 函数 MUST 返回一个 dict，包含以下字段：

- `url`: 当前解析出的完整 URL（例如 `http://localhost:8091`）
- `source`: 寻址来源，形如 `cwd-match(<name>)` / `cli-target(<x>)` / `cli-port(<n>)` / `env(<x>)` / `default`
- `ok`: bool，服务器 `/health` 端点返回 `status:ok` 时为 true
- `error`（可选）：连接失败时的异常字符串

#### Scenario: health 输出目标端口

- **WHEN** 从 `D:\unity\UnityProject\GameDesinger` 跑 `python unity_skills.py health`
- **THEN** stdout 输出 JSON，`url` 命中 registry 里该项目 port
- **AND** `source` 字段值为 `cwd-match(GameDesinger)`

### Requirement: registry.json 只读契约

客户端代码 MUST 只读不写 `~/.unity_skills/registry.json`。schema 由 unity-skills package 服务端定义，客户端 MUST 消费以下字段：

- `path` (string, 项目绝对路径)
- `name` (string, 项目名，用于 `--target` 匹配)
- `id` (string, 项目 ID，用于 `--target` 匹配)
- `port` (int, REST 服务器端口)

#### Scenario: registry.json 缺失

- **WHEN** `~/.unity_skills/registry.json` 不存在
- **THEN** `_load_registry()` 返回空 dict
- **AND** cwd 匹配失败，`_default_client` 落 8090 兜底

#### Scenario: registry.json 损坏

- **WHEN** `~/.unity_skills/registry.json` 是无效 JSON
- **THEN** `_load_registry()` 捕获 `json.JSONDecodeError` 并返回空 dict
- **AND** 不抛异常，流程继续走 8090 兜底

### Requirement: 文档禁止硬编码端口

`.claude/skills/**` 目录下所有 SKILL.md 和 references/*.md 文件 SHALL NOT 出现字面量端口号 `8090` 或 `8091` 作为 URL 路径的一部分（如 `http://localhost:8090`）。调用示例 MUST 使用 `python .claude/skills/unity-skills/scripts/unity_skills.py <skill> [params]` 形式。

允许出现的例外：
- `.claude/skills/unity-skills/scripts/unity_skills.py` 的 `DEFAULT_PORT = 8090` 常量定义 + `UNITY_URL` legacy 兼容常量（含注释说明为兼容用途）
- 讨论"默认端口是 8090"这类**说明性文本**（说明性文本不构成硬编码，因为 AI/开发者不会把它当作可运行示例）

#### Scenario: SKILL 文档提供调用示例

- **WHEN** SKILL.md 或 references/*.md 需要提供 unity-skills 调用示例
- **THEN** 示例统一用 `python .claude/skills/unity-skills/scripts/unity_skills.py <skill_name> [params]` 形式
- **AND** 不使用 `curl http://localhost:<port>/skill/<skill_name>` 形式
- **AND** 如果确实需要 curl（本次不会），必须在示例前加一句"端口从 `~/.unity_skills/registry.json` 读取"

### Requirement: 向后兼容

修复后，历史调用姿势 MUST 继续可用（无异常、无功能倒退）：

- 直接 `import unity_skills; unity_skills.call_skill(...)` 且 cwd 不在 registry 下 → 落 8090（原行为）
- `python unity_skills.py <skill> [key=value]...`（无 `--target` / `--port`）且 cwd 匹配失败 → 落 8090（原行为）
- `UnitySkills()` 无参构造 → 落 8090（原行为）

#### Scenario: 老代码不受影响

- **WHEN** 外部 Python 脚本 `import unity_skills; unity_skills.call_skill('editor_get_selection')` 且 cwd 不在任何 registry 项目下
- **THEN** 请求打到 `localhost:8090`
- **AND** 不抛任何异常
- **AND** 只在 stderr 打一行 warning（不阻塞）

