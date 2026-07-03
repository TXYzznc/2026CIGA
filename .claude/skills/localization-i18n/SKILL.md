---
name: localization-i18n
description: Unity 本地化与多语言。涵盖 Unity Localization Package 安装与配置、String Table/Asset Table、Smart String 模板、运行时切换、RTL（阿拉伯/希伯来）、字体 fallback、本地化 audit、TextMeshPro 字体生成。触发关键词：本地化、localization、i18n、多语言、Unity Localization、Smart String、RTL、阿拉伯、TMP 字体、字体 fallback、String Table。
tags: unity, localization, i18n, tmp, rtl
---

# Unity 本地化

## 何时使用

- 新项目要支持多语种，搭基础设施
- 老项目从硬编码字符串 / 自建 dictionary 迁到 Unity Localization Package
- 阿拉伯 / 希伯来 RTL 显示乱、字符不连
- TextMeshPro 缺字、显示方框 □
- 出海需要审核语言完整性（缺翻译、占位符不匹配）
- 性别 / 复数 / 货币本地化（"3 apples" / "1 apple"）

## 核心原则

- **不要在 C# 代码里写中文字面量**，全走 LocalizedString.GetLocalizedString
- **String Key 用语义命名**（`ui.menu.start`），不要用中文当 key
- **每个 key 都有占位符注释**，给翻译员看上下文
- **运行时切换语言时所有可见 UI 都要刷新**，靠 LocalizeStringEvent 自动订阅
- 字体走 **Fallback 链**，不要每个语言换 Font 资产

## 关键模式

### 模式 A：Unity Localization Package 安装

Package Manager → `com.unity.localization`（1.5+）。

目录结构：
```
Assets/Localization/
  Locales/         # zh-Hans, en, ja, ar
  StringTables/    # UI, Story, Item
  AssetTables/     # 图集、音频
  Settings.asset   # 默认 Locale、Fallback
```

`LocalizationSettings.SelectedLocale = Locale.CreateLocale(SystemLanguage.Chinese);`

### 模式 B：代码中获取字符串

```csharp
// 同步（已加载）
var s = LocalizationSettings.StringDatabase
    .GetLocalizedString("UI", "ui.menu.start");

// 异步（首次或未预加载）
var op = LocalizationSettings.StringDatabase
    .GetLocalizedStringAsync("UI", "ui.menu.start");
op.Completed += h => label.text = h.Result;

// LocalizedString 字段（推荐，可在 Inspector 拖）
[SerializeField] LocalizedString _greeting;
_greeting.Arguments = new object[] { playerName, level };
_greeting.StringChanged += s => label.text = s;
```

### 模式 C：Smart String 模板

启用 String Table 的 `Smart Format`，支持：
```
Welcome {0}, you have {1:plural:one|other} message{1:plural:|s}.
Your gold: {0:number:N0}
{gender:choose(male|female|other):He|She|They} attacks!
```

复数（ICU）：
```
en: {0} {0:plural:apple|apples}
zh: {0} 个苹果   # 中文不需要复数
ru: {0} {0:plural:яблоко|яблока|яблок}  # 俄语三态
```

### 模式 D：UI 自动绑定

TextMeshPro 上加 `LocalizeStringEvent`：拖 LocalizedString 资源 → 拖 `OnUpdateString` 到 `TMP_Text.text`。
**语言切换时无需手动刷新**，组件自动监听 `LocalizationSettings.SelectedLocaleChanged`。

### 模式 E：TMP 字体与 Fallback

CJK + 拉丁 + 阿拉伯不能一个 atlas 装下，用 Fallback：

1. 主字体 Atlas 用 Dynamic（运行时增量生成）
2. Source Font Asset 指向系统字体，覆盖到的字符随用随生
3. 静态备选：`TMP_Settings.fallbackFontAssetTable` 加 NotoSansSC、NotoSansArabic
4. 每语言一份字体 SO，切换 Locale 时换 `TMP_Settings.defaultFontAsset`

字符集导出流程：
- Window → TMP → Font Asset Creator
- Character File：从 Smart String/StringTable 导出所有出现字符
- Atlas 4096×4096 + SDF 32

### 模式 F：RTL 支持

阿拉伯/希伯来要做两件事：
1. **文本反序**：用 `ArabicSupport` 或 RTLTMPro 插件预处理字符串
2. **UI 布局镜像**：Canvas 下挂 `LayoutMirror` 脚本，根据 Locale 翻转 `Vector3.x` 与 anchorMin/anchorMax

```csharp
LocalizationSettings.SelectedLocaleChanged += locale => {
    bool rtl = locale.Identifier.Code is "ar" or "he";
    GetComponent<Canvas>().transform.localScale = new Vector3(rtl ? -1 : 1, 1, 1);
};
```

注意：UI 翻转后图标也会镜像，需要单独纠正。

### 模式 G：本地化 audit 流程

提交前跑：
1. **缺翻译**：编辑器脚本遍历所有 StringTable，列出空 entry
2. **占位符不匹配**：源 "Hello {0}" 翻译 "你好" 缺 `{0}` → 报错
3. **未引用 key**：Project-wide grep + StringTableSearch，删死 key
4. **超长文本**：德语/俄语长度可能是中文的 2x，按平台 UI 宽度告警

## 常见坑

- **Smart String 失效**：表的 `Smart Format` checkbox 没勾，或运行时拼接前缀
- **LocalizedString 在 Awake 取不到值**：异步加载未完成，监听 `StringChanged` 而非 `GetLocalizedString().Result`
- **切语言后 UI 不刷新**：手动 `setText` 而非 `LocalizeStringEvent`，或者文本被脚本覆盖
- **TMP 显示 □**：字体 Atlas 没覆盖该字符，配 Fallback 或换 Dynamic
- **阿拉伯字符不连写**：没经 ArabicSupport.ArabicFixer.Fix 处理
- **资源本地化（图、音）漏切**：用 LocalizedAsset<Sprite> 而不是代码 `Resources.Load`
- **PlayerPrefs 存 Locale Code**：用户切语言后下次启动恢复，但 Locale 列表里没了该语言会崩
- **iOS 系统语言变化**：监听 `Application.systemLanguageChanged`（部分平台无），或在 `OnApplicationFocus` 重读
- **构建后只有部分语言**：Localization Settings 的 Preload Behavior 默认 NoPreload，需要按需加载或改 PreloadAllTables

## 与其他 skill 的边界

- 与 **typeset** 的区别：那个讲字体排版美学，本 skill 讲多语种工程化
- 与 **unity-ui** 的区别：UI 讲布局/控件，本 skill 讲文本来源与切换
- 与 **font-pairing-suggester** 的区别：那个推荐字体搭配，本 skill 讲 Unity 内字体 fallback 工程
- 与 **save-serialization** 的区别：那个讲存档，本 skill 讲翻译资源加载
