
# COG项目配置表设计与生成工具

## 📖 章节导航

- **🎯 快速开始** - 基本概念和规范
- **📋 TXT格式规范** - TXT 文件编写指南
- **🔄 TXT ↔ XLSX 转换规范** - 格式转换和同步
- **🔧 AI生成配置表模板** - 生成模板示例
- **⚠️ 故障排除** - 常见问题解决
- **⚠️ 重要原则** - 工具职责边界

---

## 🎯 快速开始

### 何时使用
- 设计和创建游戏配置表
- 生成符合项目规范的配置表（强制使用资源ID）
- 生成符合 DataTableProcessor 格式的 TXT 配置文件
- 需要了解 TXT ↔ XLSX 转换规则和工作流

### ⚠️ 重要规范

**配置表设计强制规范**：
1. **禁止在配置表中填入资源路径**（如 `UI/Items/Gold.png`）
2. **必须使用资源ID**（int类型，如 `1101`）
3. **所有资源路径统一在 ResourceConfigTable 中管理**
4. **使用 ResourceIds 常量类引用资源**

**错误示例**：
```
❌ 错误：ItemTable 配置表
| ID | Name   | IconPath              |
|----|--------|-----------------------|
| 1  | 金币   | UI/Items/Gold.png     |
```

**正确示例**：
```
✅ 正确：ItemTable 配置表
| ID | Name   | IconId |
|----|--------|--------|
| 1  | 金币   | 1101   |
```

**资源ID命名规范**：
- 图标资源：`IconId` 或 `IconIds`（数组）
- 预制体资源：`PrefabId` 或 `PrefabIds`（数组）
- 特效资源：`EffectId` 或 `EffectIds`（数组）
- 材质资源：`MaterialId` 或 `MaterialIds`（数组）
- 纹理资源：`TextureId` 或 `TextureIds`（数组）
- 配置资源：`ConfigId` 或 `ConfigIds`（数组）

### 标准流程（2步）

#### 步骤1: 分析需求并生成TXT
```python
# 1. 读取相关文件了解现有系统
readMultipleFiles(["现有配置表", "相关脚本"])

# 2. 生成TXT配置表到AI工作区
fsWrite("AI工作区/配置表/TableName.txt", content)
```

**配置文件命名规范**：
- **不添加任何后缀**（如 `_扩展`、`_新建`、`_修改` 等）
- **直接使用表名**（如 `ItemTable.txt`、`PlayerDataTable.txt`、`ResourceRuleTable.txt`）
- **保持与项目中的表名一致**

**错误示例**：
```
❌ ItemTable_扩展.txt
❌ PlayerDataTable_修改.txt
❌ ResourceRuleTable_新建.txt
```

**正确示例**：
```
✅ ItemTable.txt
✅ PlayerDataTable.txt
✅ ResourceRuleTable.txt
```

**TXT格式要求**：
```
#	TableName					
#	Id		Field1	Field2	Field3
#	int		type1	type2	type3
#	ID编号	备注	说明1	说明2	说明3
	1		value1	value2	value3
```

**关键规则**：
- Tab分隔符（\t）
- 第1列：行标记（#表示元数据行，空表示数据行）
- 第2列：ID字段（主键）
- 第3列：**备注列（固定为空，用于分隔ID和其他字段），必须用Tab进行分隔**
- 第4列及以后：其他有效字段（ColorName、ColorHex、Description 等）
- 数据行第1列为空，第3列为空
- 元数据行第1列是#，第3列为空
- ⚠️ **重要**：表名行（第1行）末尾不要有多余的Tab，否则会生成多余的空列
- ⚠️ **重要**：第3列（备注列）必须存在但为空，用Tab分隔，这样第4列才是第一个有效字段
- ⚠️ **数组分隔符规则**：
  - **数组元素必须使用英文逗号 `,` 分隔**
  - **禁止使用中文顿号 `、` 或其他分隔符**
  - 正确示例：`1001,1002,1003` 或 `30001,30002,30003`
  - 错误示例：`1001、1002、1003`（会导致解析失败）
  - 适用于所有数组类型字段：`int[]`, `float[]`, `string[]` 等

#### 步骤2: 转换TXT为XLSX

用户使用外部工具将生成的TXT文件转换为XLSX格式，转换后保存到 `AAAGameData/DataTables/`。

**转换规则详解**（见下方 "TXT ↔ XLSX 转换规范" 章节）

## 📋 TXT格式规范

### 文件编码与行尾
- **编码**：UTF-8（带 BOM）
- **行尾**：Windows 格式（`\r\n`）
- **分隔符**：制表符（`\t`，不能用空格）

### DataTableProcessor兼容格式
```
行索引  内容类型     DataTableProcessor参数
0      表名行      表名在第2列
1      字段名行    name_row=1
2      字段类型行  type_row=2
3      注释行      comment_row=3
4+     数据行      content_start_row=4, id_column=1

列结构说明：
- 第1列：行标记（元数据行为#，数据行为空）
- 第2列：ID字段（主键）
- 第3列：备注列（固定为空，用于分隔ID和其他字段）
- 第4列及以后：其他有效字段
```

### 表名行规范
```
#[TAB]TableName[TAB][TAB]...(尾部制表符，对齐所有列)
```
- 开头：`#` + `\t`
- 表名之后需要补充制表符以对齐所有列数
- 例如：13列数据表 → 表名行末尾需要 10-12 个制表符

### 列名/类型/说明行规范
```
#[TAB]ID[TAB][TAB]Field1[TAB]Field2[TAB]...
```
- 开头：`#` + `\t`
- ID 后：`\t\t`（两个制表符，第3列为空）
- 之后每个字段用单个 `\t` 分隔

### 数据行规范
```
[TAB]Value1[TAB][TAB]Value2[TAB]Value3[TAB]...
```
- 开头：`\t`（一个制表符，作为缩进）
- ID 值之后：`\t\t`（两个制表符，第3列为空）
- 之后每个字段用单个 `\t` 分隔
- 空字段保留为 `\t\t`（不要省略）

### 常见格式错误

| 错误 | 原因 | 修复方法 |
|------|------|---------|
| 列对齐错乱 | 表名行末尾制表符不足 | 补充制表符使表名行与列数对齐 |
| 第3列非空 | 用空格代替制表符 | 确保用 `\t` 而非空格 |
| 数据错位 | 数据行缺少缩进制表符 | 确保数据行开头为 `\t` |
| 空字段丢失 | 省略了连续制表符 | 保留 `\t\t` 表示空列 |

### 支持的数据类型
**基础类型**：int, float, double, string, bool, datetime
**Unity类型**：vector2, vector3, vector4, quaternion, color, rect
**数组类型**：int[], float[], string[], vector3[], 等
**二维数组**：int[,], float[,], 等

详细类型列表请参考：`DATA_TYPES.md`

### 数组列自动文本格式化
转换工具会自动识别数组类型的列（字段类型包含 `[]` 或 `[,]`），并将这些列设置为 Excel 的文本格式。

**优势**：
- 防止 Excel 自动格式化数组数据（如将 `2002,2006` 格式化为 `20,022,006`）
- 保持数组数据的原始格式
- 无需手动设置单元格格式

**示例**：
```
字段类型：int[]
数据：2001,2002,2003
结果：在 Excel 中保持为 "2001,2002,2003"（文本格式）
```

## 🔧 AI生成配置表模板

```
我来生成{表名}配置表，使用DataTableProcessor兼容格式。

📁 保存位置：`AI工作区/配置表/{表名}.txt`

字段设计：
- ID (int): 唯一标识
- {字段名} ({类型}): {说明}

TXT内容（注意第3列为备注列，固定为空）：
#	{TableName}													
#	ID		{Field1}	{Field2}
#	int		{Type1}	{Type2}
#	ID编号	备注	{说明1}	{说明2}
	1		{值1}	{值2}
```

## ⚠️ 故障排除

### Windows Bash环境问题
`executePwsh`工具存在工作目录管理问题，推荐使用 `controlPwshProcess` + 临时脚本方式。

详细解决方案请参考：`TROUBLESHOOTING.md`

### 常见问题
- **格式错误**：检查Tab分隔符和空列
- **转换失败**：确认UTF-8编码
- **服务器连接失败**：确认TCP服务器已启动

## ⚠️ 重要原则

### 核心职责
**这个SKILL的唯一职责：设计和生成符合项目规范的TXT格式配置表**

✅ **应该做的**：
1. 分析需求，设计配置表字段
2. 生成 TXT 配置表到 `AI工作区/配置表/`
3. 验证 TXT 格式的正确性
4. 提供配置表设计指导和最佳实践

❌ **不应该做的**：
1. **不负责 TXT 转 XLSX 的转换**（用户手动执行）
2. **不启动或管理 TCP 服务器**
3. **不调用 MCP 工具执行转换**
4. **不自动复制文件到项目其他目录**
5. **不假设用户的工作流程**

### 为什么这样设计？

**简化原则**：
- 这个SKILL专注于一件事：配置表的设计和TXT生成
- TXT 转 XLSX 的转换交给用户手动执行（使用Excel、Python脚本等）
- 降低工具的复杂度，提高可维护性

**⚠️ 本 skill 描述的工作流（TXT → XLSX → AAAGameData/DataTables/）属于另一个项目历史污染，本项目不再使用。本项目实际流程见 [.claude/CLAUDE.md](../../../CLAUDE.md) §七「DataTable」：**

```
1. 用户在 Assets/Resources/DataTable/<Name>.json 直接编辑 JSON
2. 用户在 Unity 菜单 Tools/DataTable/生成全部配置表代码
3. DataTableGenerator 输出 Assets/Scripts/DataTable/<Name>.cs
```

**完全没有 TXT、XLSX、AAAGameData/DataTables/。本 skill 仅作历史参考保留，禁止 agent 按此流程执行。**

### 经验教训

1. **明确任务边界** - 工具的职责是设计和生成，不是部署
2. **避免过度复杂** - 不要集成太多功能，保持工具简单
3. **让用户决定** - 不要假设用户的后续操作

## 🔄 TXT ↔ XLSX 转换规范

### 转换概述

TXT 和 XLSX 是同一配置表的两种格式：
- **TXT**：制表符分隔的源格式，用于版本控制和 DataTableProcessor
- **XLSX**：Excel 格式，便于手动编辑和查看

两个文件必须保持数据一致。

### 映射关系

| 方面 | TXT 格式 | XLSX 格式 |
|------|---------|----------|
| **编码** | UTF-8（BOM）| 自动（通常 UTF-8） |
| **行尾** | Windows（`\r\n`） | 自动 |
| **分隔符** | TAB（`\t`） | 单元格列 |
| **第1列** | 行标记（`#` 或空）| `#` 或空 |
| **第2列** | ID | ID |
| **第3列** | 空（对齐用） | 空（对齐用） |
| **后续列** | 各字段数据 | 各字段数据 |

### 转换步骤

#### TXT → XLSX

```python
import openpyxl

# 1. 读取 TXT 文件
rows = []
with open('TableName.txt', 'r', encoding='utf-8-sig') as f:
    for line in f:
        rows.append(line.rstrip('\n\r').split('\t'))

# 2. 创建 XLSX
wb = openpyxl.Workbook()
ws = wb.active

# 3. 写入数据
for row_idx, row in enumerate(rows, 1):
    for col_idx, val in enumerate(row, 1):
        ws.cell(row=row_idx, column=col_idx, value=val if val else None)

# 4. 数组列设置为文本格式
for row_idx in range(4, len(rows) + 1):  # 从第5行开始（数据行）
    for col_idx in range(1, len(rows[0]) + 1):
        cell = ws.cell(row=row_idx, column=col_idx)
        if cell.value and isinstance(cell.value, str) and ',' in cell.value:
            cell.number_format = '@'  # 文本格式

# 5. 保存
wb.save('TableName.xlsx')
```

#### XLSX → TXT

```python
import openpyxl

# 1. 读取 XLSX 文件
wb = openpyxl.load_workbook('TableName.xlsx')
ws = wb.active

# 2. 提取所有行
rows = []
for row in ws.iter_rows(values_only=True):
    rows.append(row)

# 3. 转换为 TAB 分隔格式
lines = []
for row in rows:
    row_str = '\t'.join(str(val) if val else '' for val in row)
    lines.append(row_str)

# 4. 保存为 TXT
with open('TableName.txt', 'w', encoding='utf-8-sig', newline='') as f:
    for line in lines:
        f.write(line + '\r\n')
```

### 验证转换结果

```python
import openpyxl

# 对比 TXT 和 XLSX 是否一致
def verify_consistency(txt_path, xlsx_path):
    # 读取 TXT
    with open(txt_path, 'r', encoding='utf-8-sig') as f:
        txt_rows = [line.rstrip('\n\r').split('\t') for line in f]
    
    # 读取 XLSX
    wb = openpyxl.load_workbook(xlsx_path)
    ws = wb.active
    xlsx_rows = []
    for row in ws.iter_rows(values_only=True):
        xlsx_rows.append([str(val) if val else '' for val in row])
    
    # 对比
    for i, (txt_row, xlsx_row) in enumerate(zip(txt_rows, xlsx_rows)):
        if len(txt_row) != len(xlsx_row):
            print(f"❌ 第 {i+1} 行：列数不匹配 TXT={len(txt_row)}, XLSX={len(xlsx_row)}")
        else:
            for j, (txt_val, xlsx_val) in enumerate(zip(txt_row, xlsx_row)):
                if txt_val.strip() != xlsx_val.strip():
                    print(f"❌ 第 {i+1} 行，第 {j+1} 列不匹配：TXT='{txt_val}', XLSX='{xlsx_val}'")
    
    print("✅ 验证完成")

verify_consistency('TableName.txt', 'TableName.xlsx')
```

### 常见问题

| 问题 | 原因 | 解决 |
|------|------|------|
| 列数不匹配 | TXT 缺少制表符 | 确保每列间都有 `\t`，空列也要有 |
| 数据错位 | 某行缺少对齐制表符 | 检查第 3 列（对齐列）是否为空 |
| Excel 格式错乱 | 数组列自动格式化 | 数组列需设置为文本格式 |
| 字符显示错误 | 编码问题 | 使用 UTF-8 with BOM，不要用 ANSI |

### 同步工作流

```
修改需求
    ↓
更新 TXT → 生成 XLSX（脚本转换）
    ↓
在 Excel 中验证（可选手动微调）
    ↓
重新生成 TXT（脚本反向转换）
    ↓
提交 TXT 到版本控制
```

**推荐**：保持 TXT 作为主源文件，XLSX 作为二级格式用于编辑查看。

---

## 📚 相关文档

- `README.md` - 完整使用说明
- `DATA_TYPES.md` - 数据类型详细列表
- `TROUBLESHOOTING.md` - 故障排除指南
- `EXAMPLES.md` - 配置表示例集合
