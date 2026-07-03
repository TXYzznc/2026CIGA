#!/usr/bin/env node

const fs = require("fs");

function readStdin() {
  return new Promise((resolve) => {
    let data = "";
    process.stdin.setEncoding("utf8");
    process.stdin.on("data", (chunk) => {
      data += chunk;
    });
    process.stdin.on("end", () => resolve(data));
    process.stdin.on("error", () => resolve(""));
  });
}

function emit(eventName, additionalContext) {
  if (!additionalContext) return;
  console.log(JSON.stringify({
    hookSpecificOutput: {
      hookEventName: eventName,
      additionalContext,
    },
  }));
}

function parsePrompt(raw) {
  try {
    return JSON.parse(raw || "{}").prompt || "";
  } catch {
    return "";
  }
}

function baseContext() {
  return [
    "始终使用中文回答。",
    "优先简单方案，不做过度工程。",
    "实现 Unity 代码前先查询当前项目真实结构；本模板不预设 InputModule、EventBus、DataTableGenerator 或任何运行时框架。",
    "Claude 工作流是语义源；Codex 执行时按 AGENTS.md 的 Codex 适配规则等价落地。",
  ].join("\n");
}

function sessionContext() {
  try {
    const agents = fs.readFileSync("AGENTS.md", "utf8");
    return `【自动注入 AGENTS.md】\n\n${agents}`;
  } catch {
    return "";
  }
}

function decisionGateContext(prompt) {
  const re = /设计|架构|重构|大改|重写|GDD|PRD|系统|范式|方案|思路/;
  if (!re.test(prompt)) return "";

  return [
    "检测到大型决策关键词。先按 grill-me / grill-with-docs 的问题框架澄清：",
    "1) 目标 2) 关键决策 3) 边界 4) 验收标准 5) 约束。",
    "澄清后再评估是否需要 OpenSpec change；执行期只在共识冲突、不可逆变更或项目协作契约变更时中断用户。",
  ].join("\n");
}

function graphifyContext(prompt) {
  if (!prompt.trim().startsWith("/graphify")) return "";
  return "检测到 /graphify。Codex 中应优先使用 graphify-windows skill，并在做其他操作前读取该 skill 的 SKILL.md。";
}

async function main() {
  const mode = process.argv[2] || "prompt";
  if (mode === "session") {
    emit("SessionStart", sessionContext());
    return;
  }

  const prompt = parsePrompt(await readStdin());
  emit("UserPromptSubmit", [
    baseContext(),
    decisionGateContext(prompt),
    graphifyContext(prompt),
  ].filter(Boolean).join("\n\n"));
}

main().catch((err) => {
  console.error(err && err.stack ? err.stack : String(err));
  process.exit(0);
});
