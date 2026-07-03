---
name: agent-browser
description: 面向AI Agent的浏览器自动化CLI工具。当用户需要与网站交互时使用，包括页面导航、表单填写、按钮点击、截图、数据提取、Web应用测试或任何浏览器任务自动化。触发场景包括请求“打开网站”、“填写表单”、“点击按钮”、“截图”、“从页面抓取数据”、“测试此Web应用”、“登录网站”、“自动化浏览器操作”或任何需要程序化Web交互的任务。
allowed-tools: Bash(agent-browser:*)
tags: browser-automation, ai-agent-cli, web-interaction, data-extraction, session-management
tags_cn: 浏览器自动化, AI Agent CLI工具, Web页面交互, 数据提取, 会话管理
---

# 基于agent-browser的浏览器自动化

## 核心工作流

所有浏览器自动化都遵循以下模式：

1. **导航**：`agent-browser open <url>`
2. **快照**：`agent-browser snapshot -i`（获取元素引用，如`@e1`、`@e2`）
3. **交互**：使用引用进行点击、填写、选择操作
4. **重新快照**：导航或DOM变更后，获取最新的元素引用

```bash
agent-browser open https://example.com/form
agent-browser snapshot -i
# 输出：@e1 [input type="email"], @e2 [input type="password"], @e3 [button] "Submit"

agent-browser fill @e1 "user@example.com"
agent-browser fill @e2 "password123"
agent-browser click @e3
agent-browser wait --load networkidle
agent-browser snapshot -i  # 检查结果
```

## 核心命令

```bash
# 导航
agent-browser open <url>              # 导航（别名：goto、navigate）
agent-browser close                   # 关闭浏览器

# 快照
agent-browser snapshot -i             # 获取带引用的交互元素（推荐）
agent-browser snapshot -s "#selector" # 限定到CSS选择器范围

# 交互（使用快照中的@引用）
agent-browser click @e1               # 点击元素
agent-browser fill @e2 "text"         # 清空并输入文本
agent-browser type @e2 "text"         # 输入文本不清空原有内容
agent-browser select @e1 "option"     # 选择下拉选项
agent-browser check @e1               # 勾选复选框
agent-browser press Enter             # 按下按键
agent-browser scroll down 500         # 向下滚动页面

# 获取信息
agent-browser get text @e1            # 获取元素文本
agent-browser get url                 # 获取当前URL
agent-browser get title               # 获取页面标题

# 等待
agent-browser wait @e1                # 等待元素加载
agent-browser wait --load networkidle # 等待网络空闲
agent-browser wait --url "**/page"    # 等待URL匹配指定模式
agent-browser wait 2000               # 等待指定毫秒数

# 捕获
agent-browser screenshot              # 截图保存到临时目录
agent-browser screenshot --full       # 整页截图
agent-browser pdf output.pdf          # 保存为PDF
```

## 常见模式

### 表单提交

```bash
agent-browser open https://example.com/signup
agent-browser snapshot -i
agent-browser fill @e1 "Jane Doe"
agent-browser fill @e2 "jane@example.com"
agent-browser select @e3 "California"
agent-browser check @e4
agent-browser click @e5
agent-browser wait --load networkidle
```

### 带状态持久化的身份验证

```bash
# 登录一次并保存状态
agent-browser open https://app.example.com/login
agent-browser snapshot -i
agent-browser fill @e1 "$USERNAME"
agent-browser fill @e2 "$PASSWORD"
agent-browser click @e3
agent-browser wait --url "**/dashboard"
agent-browser state save auth.json

# 在后续会话中复用
agent-browser state load auth.json
agent-browser open https://app.example.com/dashboard
```

### 数据提取

```bash
agent-browser open https://example.com/products
agent-browser snapshot -i
agent-browser get text @e5           # 获取特定元素文本
agent-browser get text body > page.txt  # 获取页面所有文本

# 输出JSON格式以便解析
agent-browser snapshot -i --json
agent-browser get text @e1 --json
```

### 并行会话

```bash
agent-browser --session site1 open https://site-a.com
agent-browser --session site2 open https://site-b.com

agent-browser --session site1 snapshot -i
agent-browser --session site2 snapshot -i

agent-browser session list
```

### 可视化浏览器（调试用）

```bash
agent-browser --headed open https://example.com
agent-browser highlight @e1          # 高亮元素
agent-browser record start demo.webm # 录制会话
```

### iOS模拟器（移动端Safari）

```bash
# 列出可用的iOS模拟器
agent-browser device list

# 在指定设备上启动Safari
agent-browser -p ios --device "iPhone 16 Pro" open https://example.com

# 与桌面端相同工作流 - 快照、交互、重新快照
agent-browser -p ios snapshot -i
agent-browser -p ios tap @e1          # 点击（click的别名）
agent-browser -p ios fill @e2 "text"
agent-browser -p ios swipe up         # 移动端特定手势

# 截图
agent-browser -p ios screenshot mobile.png

# 关闭会话（关闭模拟器）
agent-browser -p ios close
```

**要求**：安装Xcode的macOS系统，以及Appium（`npm install -g appium && appium driver install xcuitest`）

**真实设备**：如果预先配置好，可在物理iOS设备上运行。使用`--device "<UDID>"`，其中UDID可从`xcrun xctrace list devices`获取。

## 引用生命周期（重要）

引用（`@e1`、`@e2`等）会在页面变更时失效。在以下操作后务必重新快照：

- 点击链接或按钮导致页面导航
- 表单提交
- 动态内容加载（下拉菜单、模态框）

```bash
agent-browser click @e5              # 导航到新页面
agent-browser snapshot -i            # 务必重新快照
agent-browser click @e1              # 使用新的引用
```

## 语义定位器（引用的替代方案）

当引用不可用或不可靠时，使用语义定位器：

```bash
agent-browser find text "Sign In" click
agent-browser find label "Email" fill "user@test.com"
agent-browser find role button click --name "Submit"
agent-browser find placeholder "Search" type "query"
agent-browser find testid "submit-btn" click
```

## 深入文档

| 参考文档 | 使用场景 |
|-----------|-------------|
| [references/commands.md](references/commands.md) | 包含所有选项的完整命令参考 |
| [references/snapshot-refs.md](references/snapshot-refs.md) | 引用生命周期、失效规则、故障排除 |
| [references/session-management.md](references/session-management.md) | 并行会话、状态持久化、并发抓取 |
| [references/authentication.md](references/authentication.md) | 登录流程、OAuth、2FA处理、状态复用 |
| [references/video-recording.md](references/video-recording.md) | 录制工作流用于调试和文档记录 |
| [references/proxy-support.md](references/proxy-support.md) | 代理配置、地域测试、轮换代理 |

## 即用型模板

| 模板 | 描述 |
|----------|-------------|
| [templates/form-automation.sh](templates/form-automation.sh) | 带验证的表单填写自动化 |
| [templates/authenticated-session.sh](templates/authenticated-session.sh) | 一次登录，复用状态 |
| [templates/capture-workflow.sh](templates/capture-workflow.sh) | 带截图的内容提取 |

```bash
./templates/form-automation.sh https://example.com/form
./templates/authenticated-session.sh https://app.example.com/login
./templates/capture-workflow.sh https://example.com ./output
```