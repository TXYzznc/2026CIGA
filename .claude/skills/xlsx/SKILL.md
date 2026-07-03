---
name: xlsx
description: 支持公式、格式设置、数据分析和可视化的全面电子表格创建、编辑与分析。当Claude需要处理电子表格（.xlsx、.xlsm、.csv、.tsv等）时，可用于：(1)
  创建包含公式和格式的新电子表格；(2) 读取或分析数据；(3) 修改现有电子表格同时保留公式；(4) 在电子表格中进行数据分析与可视化；(5) 重新计算公式
license: Proprietary. LICENSE.txt has complete terms
tags: spreadsheet-automation, excel-formula-management, pandas-data-analysis, openpyxl-usage,
  recalc-script
tags_cn: 电子表格自动化, Excel公式管理, Pandas数据分析, Openpyxl使用, Recalc脚本操作
---

# 输出要求

## 所有Excel文件

### 无公式错误
- 所有交付的Excel模型必须不存在任何公式错误（#REF!、#DIV/0!、#VALUE!、#N/A、#NAME?）

### 保留现有模板（更新模板时）
- 修改文件时，需研究并严格匹配现有格式、样式和规范
- 不得对已有固定格式的文件套用标准化格式
- 现有模板规范始终优先于本指南

## 财务模型

### 颜色编码标准
除非用户或现有模板另有说明

#### 行业标准颜色规范
- **蓝色文本（RGB: 0,0,255）**：硬编码输入值，以及用户可针对不同场景修改的数值
- **黑色文本（RGB: 0,0,0）**：所有公式和计算结果
- **绿色文本（RGB: 0,128,0）**：指向同一工作簿内其他工作表的链接
- **红色文本（RGB: 255,0,0）**：指向其他外部文件的链接
- **黄色背景（RGB: 255,255,0）**：需要重点关注的关键假设或需更新的单元格

### 数字格式标准

#### 必填格式规则
- **年份**：格式设为文本字符串（例如："2024" 而非 "2,024"）
- **货币**：使用$#,##0格式；必须在表头中指定单位（如"Revenue ($mm)"）
- **零值**：通过数字格式将所有零值显示为"-"，包括百分比（例如："$#,##0;($#,##0);-"）
- **百分比**：默认使用0.0%格式（保留一位小数）
- **倍数**：估值倍数（EV/EBITDA、P/E）格式设为0.0x
- **负数**：使用括号(123)表示，而非负号-123

### 公式构建规则

#### 假设条件放置
- 所有假设条件（增长率、利润率、倍数等）需放在单独的假设单元格中
- 公式中使用单元格引用而非硬编码值
- 示例：使用=B5*(1+$B$6) 而非=B5*1.05

#### 公式错误预防
- 验证所有单元格引用是否正确
- 检查范围是否存在差一错误
- 确保所有预测期间的公式保持一致
- 使用边缘案例进行测试（零值、负数）
- 验证不存在意外的循环引用

#### 硬编码值的文档要求
- 在单元格旁添加注释（如果在表格末尾）。格式："Source: [系统/文档], [日期], [具体引用], [适用的URL]"
- 示例：
  - "Source: Company 10-K, FY2024, Page 45, Revenue Note, [SEC EDGAR URL]"
  - "Source: Company 10-Q, Q2 2025, Exhibit 99.1, [SEC EDGAR URL]"
  - "Source: Bloomberg Terminal, 8/15/2025, AAPL US Equity"
  - "Source: FactSet, 8/20/2025, Consensus Estimates Screen"

# XLSX创建、编辑与分析

## 概述

用户可能会要求你创建、编辑或分析.xlsx文件的内容。针对不同任务，你可以使用不同的工具和工作流。

## 重要要求

**公式重计算需使用LibreOffice**：可假设已安装LibreOffice，用于通过`recalc.py`脚本重新计算公式值。该脚本会在首次运行时自动配置LibreOffice

## 读取与分析数据

### 使用pandas进行数据分析
对于数据分析、可视化和基础操作，使用**pandas**，它提供强大的数据处理能力：

```python
import pandas as pd

# 读取Excel
df = pd.read_excel('file.xlsx')  # 默认：第一个工作表
all_sheets = pd.read_excel('file.xlsx', sheet_name=None)  # 所有工作表以字典形式返回

# 分析数据
df.head()      # 预览数据
df.info()      # 列信息
df.describe()  # 统计信息

# 写入Excel
df.to_excel('output.xlsx', index=False)
```

## Excel文件工作流

## 关键要求：使用公式而非硬编码值

**始终使用Excel公式，而非在Python中计算值后进行硬编码。** 这样可确保电子表格保持动态可更新。

### ❌ 错误示例 - 硬编码计算值
```python
# 错误：在Python中计算后硬编码结果
total = df['Sales'].sum()
sheet['B10'] = total  # 硬编码为5000

# 错误：在Python中计算增长率
growth = (df.iloc[-1]['Revenue'] - df.iloc[0]['Revenue']) / df.iloc[0]['Revenue']
sheet['C5'] = growth  # 硬编码为0.15

# 错误：在Python中计算平均值
avg = sum(values) / len(values)
sheet['D20'] = avg  # 硬编码为42.5
```

### ✅ 正确示例 - 使用Excel公式
```python
# 正确：让Excel计算总和
sheet['B10'] = '=SUM(B2:B9)'

# 正确：增长率以Excel公式表示
sheet['C5'] = '=(C4-C2)/C2'

# 正确：使用Excel函数计算平均值
sheet['D20'] = '=AVERAGE(D2:D19)'
```

这适用于所有计算 - 总计、百分比、比率、差值等。当源数据更改时，电子表格应能重新计算结果。

## 通用工作流
1. **选择工具**：数据处理用pandas，公式/格式设置用openpyxl
2. **创建/加载**：创建新工作簿或加载现有文件
3. **修改**：添加/编辑数据、公式和格式
4. **保存**：写入文件
5. **重新计算公式（使用公式时必须执行）**：使用recalc.py脚本
   ```bash
   python recalc.py output.xlsx
   ```
6. **验证并修复错误**：
   - 脚本会返回包含错误详情的JSON
   - 如果`status`为`errors_found`，查看`error_summary`获取具体错误类型和位置
   - 修复已识别的错误并重新计算
   - 常见需修复的错误：
     - `#REF!`：无效单元格引用
     - `#DIV/0!`：除以零
     - `#VALUE!`：公式中数据类型错误
     - `#NAME?`：无法识别的公式名称

### 创建新Excel文件

```python
# 使用openpyxl处理公式和格式
from openpyxl import Workbook
from openpyxl.styles import Font, PatternFill, Alignment

wb = Workbook()
sheet = wb.active

# 添加数据
sheet['A1'] = 'Hello'
sheet['B1'] = 'World'
sheet.append(['Row', 'of', 'data'])

# 添加公式
sheet['B2'] = '=SUM(A1:A10)'

# 设置格式
sheet['A1'].font = Font(bold=True, color='FF0000')
sheet['A1'].fill = PatternFill('solid', start_color='FFFF00')
sheet['A1'].alignment = Alignment(horizontal='center')

# 列宽设置
sheet.column_dimensions['A'].width = 20

wb.save('output.xlsx')
```

### 编辑现有Excel文件

```python
# 使用openpyxl保留公式和格式
from openpyxl import load_workbook

# 加载现有文件
wb = load_workbook('existing.xlsx')
sheet = wb.active  # 或使用wb['SheetName']指定具体工作表

# 处理多个工作表
for sheet_name in wb.sheetnames:
    sheet = wb[sheet_name]
    print(f"Sheet: {sheet_name}")

# 修改单元格
sheet['A1'] = 'New Value'
sheet.insert_rows(2)  # 在位置2插入行
sheet.delete_cols(3)  # 删除第3列

# 添加新工作表
new_sheet = wb.create_sheet('NewSheet')
new_sheet['A1'] = 'Data'

wb.save('modified.xlsx')
```

## 重新计算公式

由openpyxl创建或修改的Excel文件中，公式以字符串形式存在但未计算出值。使用提供的`recalc.py`脚本重新计算公式：

```bash
python recalc.py <excel_file> [timeout_seconds]
```

示例：
```bash
python recalc.py output.xlsx 30
```

该脚本：
- 首次运行时自动设置LibreOffice宏
- 重新计算所有工作表中的所有公式
- 扫描所有单元格查找Excel错误（#REF!、#DIV/0!等）
- 返回包含详细错误位置和数量的JSON
- 支持Linux和macOS系统

## 公式验证清单

快速检查确保公式正常工作：

### 核心验证
- [ ] **测试2-3个示例引用**：构建完整模型前，验证引用是否能获取正确值
- [ ] **列映射**：确认Excel列匹配（例如：第64列=BL，而非BK）
- [ ] **行偏移**：记住Excel行是从1开始索引的（DataFrame第5行 = Excel第6行）

### 常见陷阱
- [ ] **NaN处理**：使用`pd.notna()`检查空值
- [ ] **右侧列**：财年数据通常在第50列之后
- [ ] **多匹配项**：搜索所有匹配结果，而非仅第一个
- [ ] **除以零**：在公式中使用`/`前检查分母是否为零（避免#DIV/0!）
- [ ] **错误引用**：验证所有单元格引用指向预期的单元格（避免#REF!）
- [ ] **跨工作表引用**：使用正确格式（Sheet1!A1）链接工作表

### 公式测试策略
- [ ] **从小规模开始**：先在2-3个单元格上测试公式，再广泛应用
- [ ] **验证依赖项**：检查公式中引用的所有单元格是否存在
- [ ] **测试边缘案例**：包含零值、负值和极大值

### 解读recalc.py输出
脚本返回包含错误详情的JSON：
```json
{
  "status": "success",           // 或"errors_found"
  "total_errors": 0,              // 错误总数
  "total_formulas": 42,           // 文件中的公式数量
  "error_summary": {              // 仅当存在错误时显示
    "#REF!": {
      "count": 2,
      "locations": ["Sheet1!B5", "Sheet1!C10"]
    }
  }
}
```

## 最佳实践

### 库选择
- **pandas**：最适合数据分析、批量操作和简单数据导出
- **openpyxl**：最适合复杂格式设置、公式处理和Excel特定功能

### 使用openpyxl的注意事项
- 单元格索引从1开始（row=1, column=1对应单元格A1）
- 使用`data_only=True`读取计算后的值：`load_workbook('file.xlsx', data_only=True)`
- **警告**：如果以`data_only=True`打开并保存文件，公式会被值替换且永久丢失
- 处理大文件：读取时使用`read_only=True`，写入时使用`write_only=True`
- 公式会被保留但不会自动计算 - 使用recalc.py更新值

### 使用pandas的注意事项
- 指定数据类型以避免推断问题：`pd.read_excel('file.xlsx', dtype={'id': str})`
- 处理大文件时，读取指定列：`pd.read_excel('file.xlsx', usecols=['A', 'C', 'E'])`
- 正确处理日期：`pd.read_excel('file.xlsx', parse_dates=['date_column'])`

## 代码风格指南
**重要**：生成用于Excel操作的Python代码时：
- 编写简洁、精简的Python代码，避免不必要的注释
- 避免冗长的变量名和冗余操作
- 避免不必要的打印语句

**对于Excel文件本身**：
- 为包含复杂公式或重要假设的单元格添加注释
- 为硬编码值添加数据源文档
- 为关键计算和模型部分添加说明