
# DOCX创建、编辑与分析

## 概述

.docx文件是一个包含XML文件的ZIP压缩包。

## 快速参考

| 任务 | 处理方式 |
|------|----------|
| 读取/分析内容 | `pandoc` 或解压获取原始XML |
| 创建新文档 | 使用`docx-js` - 详见下方“创建新文档”部分 |
| 编辑现有文档 | 解压 → 编辑XML → 重新打包 - 详见下方“编辑现有文档”部分 |

### 将.doc转换为.docx

旧版.doc文件必须先转换才能编辑：

```bash
python scripts/office/soffice.py --headless --convert-to docx document.doc
```

### 读取内容

```bash
# 提取带修订记录的文本
pandoc --track-changes=all document.docx -o output.md

# 访问原始XML
python scripts/office/unpack.py document.docx unpacked/
```

### 转换为图片

```bash
python scripts/office/soffice.py --headless --convert-to pdf document.docx
pdftoppm -jpeg -r 150 document.pdf page
```

### 接受修订内容

要生成已接受所有修订的干净文档（需要LibreOffice）：

```bash
python scripts/accept_changes.py input.docx output.docx
```

---

## 创建新文档

使用JavaScript生成.docx文件，然后进行验证。安装：`npm install -g docx`

### 初始化
```javascript
const { Document, Packer, Paragraph, TextRun, Table, TableRow, TableCell, ImageRun,
        Header, Footer, AlignmentType, PageOrientation, LevelFormat, ExternalHyperlink,
        TableOfContents, HeadingLevel, BorderStyle, WidthType, ShadingType,
        VerticalAlign, PageNumber, PageBreak } = require('docx');

const doc = new Document({ sections: [{ children: [/* 内容 */] }] });
Packer.toBuffer(doc).then(buffer => fs.writeFileSync("doc.docx", buffer));
```

### 验证
创建文件后，对其进行验证。如果验证失败，解压文件、修复XML并重新打包。
```bash
python scripts/office/validate.py doc.docx
```

### 页面尺寸

```javascript
// 重要提示：docx-js默认使用A4纸张，而非美国信纸（US Letter）
// 请始终显式设置页面尺寸以确保结果一致
sections: [{
  properties: {
    page: {
      size: {
        width: 12240,   // 8.5英寸，单位为DXA
        height: 15840   // 11英寸，单位为DXA
      },
      margin: { top: 1440, right: 1440, bottom: 1440, left: 1440 } // 1英寸边距
    }
  },
  children: [/* 内容 */]
}]
```

**常见页面尺寸（DXA单位，1440 DXA = 1英寸）：**

| 纸张类型 | 宽度 | 高度 | 内容宽度（1英寸边距） |
|-------|-------|--------|---------------------------|
| US Letter | 12,240 | 15,840 | 9,360 |
| A4（默认） | 11,906 | 16,838 | 9,026 |

**横向排版：** docx-js会在内部交换宽高，因此传入纵向尺寸即可，由它处理交换：
```javascript
size: {
  width: 12240,   // 传入短边作为宽度
  height: 15840,  // 传入长边作为高度
  orientation: PageOrientation.LANDSCAPE  // docx-js会在XML中自动交换它们
},
// 内容宽度 = 15840 - 左边距 - 右边距（使用长边计算）
```

### 样式（覆盖内置标题）

使用Arial作为默认字体（通用支持）。标题保持黑色以保证可读性。

```javascript
const doc = new Document({
  styles: {
    default: { document: { run: { font: "Arial", size: 24 } } }, // 默认12号字体
    paragraphStyles: [
      // 重要提示：使用精确的ID来覆盖内置样式
      { id: "Heading1", name: "Heading 1", basedOn: "Normal", next: "Normal", quickFormat: true,
        run: { size: 32, bold: true, font: "Arial" },
        paragraph: { spacing: { before: 240, after: 240 }, outlineLevel: 0 } }, // 生成目录需要设置outlineLevel
      { id: "Heading2", name: "Heading 2", basedOn: "Normal", next: "Normal", quickFormat:true,
        run: { size: 28, bold: true, font: "Arial" },
        paragraph: { spacing: { before: 180, after: 180 }, outlineLevel: 1 } },
    ]
  },
  sections: [{
    children: [
      new Paragraph({ heading: HeadingLevel.HEADING_1, children: [new TextRun("标题")] }),
    ]
  }]
});
```

### 列表（绝不要使用Unicode项目符号）

```javascript
// ❌ 错误：绝不要手动插入项目符号字符
new Paragraph({ children: [new TextRun("• 项目")] })  // 错误示例
new Paragraph({ children: [new TextRun("\\u2022 项目")] })  // 错误示例

// ✅ 正确：使用带编号配置的LevelFormat.BULLET
const doc = new Document({
  numbering: {
    config: [
      { reference: "bullets",
        levels: [{ level: 0, format: LevelFormat.BULLET, text: "•", alignment: AlignmentType.LEFT,
          style: { paragraph: { indent: { left: 720, hanging: 360 } } } }] },
      { reference: "numbers",
        levels: [{ level: 0, format: LevelFormat.DECIMAL, text: "%1.", alignment: AlignmentType.LEFT,
          style: { paragraph: { indent: { left: 720, hanging: 360 } } } }] },
    ]
  },
  sections: [{
    children: [
      new Paragraph({ numbering: { reference: "bullets", level: 0 },
        children: [new TextRun("项目符号项")] }),
      new Paragraph({ numbering: { reference: "numbers", level: 0 },
        children: [new TextRun("编号项")] }),
    ]
  }]
});

// ⚠️ 每个引用会创建独立的编号序列
// 相同引用：编号连续（1,2,3 之后是4,5,6）
// 不同引用：编号重新开始（1,2,3 之后是1,2,3）
```

### 表格

**关键注意事项：表格需要双宽度设置** - 同时在表格上设置`columnWidths`和每个单元格上设置`width`。如果不同时设置，表格在部分平台上会显示异常。

```javascript
// 重要提示：请始终设置表格宽度以确保渲染一致
// 重要提示：使用ShadingType.CLEAR（而非SOLID）以避免黑色背景
const border = { style: BorderStyle.SINGLE, size: 1, color: "CCCCCC" };
const borders = { top: border, bottom: border, left: border, right: border };

new Table({
  width: { size: 9360, type: WidthType.DXA }, // 始终使用DXA单位（百分比在Google Docs中会失效）
  columnWidths: [4680, 4680], // 总和必须等于表格宽度（DXA单位：1440 = 1英寸）
  rows: [
    new TableRow({
      children: [
        new TableCell({
          borders,
          width: { size: 4680, type: WidthType.DXA }, // 同时在每个单元格上设置宽度
          shading: { fill: "D5E8F0", type: ShadingType.CLEAR }, // 使用CLEAR而非SOLID
          margins: { top: 80, bottom: 80, left: 120, right: 120 }, // 单元格内边距（内部填充，不增加单元格宽度）
          children: [new Paragraph({ children: [new TextRun("单元格")] })]
        })
      ]
    })
  ]
})
```

**表格宽度计算：**

始终使用`WidthType.DXA` — `WidthType.PERCENTAGE`在Google Docs中会失效。

```javascript
// 表格宽度 = 列宽度总和 = 内容宽度
// 带1英寸边距的美国信纸：12240 - 2880 = 9360 DXA
width: { size: 9360, type: WidthType.DXA },
columnWidths: [7000, 2360]  // 总和必须等于表格宽度
```

**宽度规则：**
- **始终使用`WidthType.DXA`** — 绝不要使用`WidthType.PERCENTAGE`（与Google Docs不兼容）
- 表格宽度必须等于`columnWidths`的总和
- 单元格`width`必须与对应的`columnWidth`匹配
- 单元格`margins`是内部填充 - 会缩小内容区域，不会增加单元格宽度
- 要创建全宽表格：使用内容宽度（页面宽度减去左右边距）

### 图片

```javascript
// 重要提示：必须指定type参数
new Paragraph({
  children: [new ImageRun({
    type: "png", // 必填：png, jpg, jpeg, gif, bmp, svg
    data: fs.readFileSync("image.png"),
    transformation: { width: 200, height: 150 },
    altText: { title: "标题", description: "描述", name: "名称" } // 这三个属性都必须设置
  })]
})
```

### 分页符

```javascript
// 重要提示：PageBreak必须放在Paragraph内部
new Paragraph({ children: [new PageBreak()] })

// 或者使用pageBreakBefore属性
new Paragraph({ pageBreakBefore: true, children: [new TextRun("新页面")] })
```

### 目录

```javascript
// 重要提示：标题必须仅使用HeadingLevel，不能使用自定义样式
new TableOfContents("目录", { hyperlink: true, headingStyleRange: "1-3" })
```

### 页眉/页脚

```javascript
sections: [{
  properties: {
    page: { margin: { top: 1440, right: 1440, bottom: 1440, left: 1440 } } // 1440 = 1英寸
  },
  headers: {
    default: new Header({ children: [new Paragraph({ children: [new TextRun("页眉")] })] })
  },
  footers: {
    default: new Footer({ children: [new Paragraph({
      children: [new TextRun("第 "), new TextRun({ children: [PageNumber.CURRENT] }), new TextRun(" 页")]
    })] })
  },
  children: [/* 内容 */]
}]
```

### docx-js使用关键规则

- **显式设置页面尺寸** - docx-js默认使用A4纸；针对美国地区的文档请使用US Letter（12240 x 15840 DXA）
- **横向排版：传入纵向尺寸** - docx-js会在内部交换宽高；将短边作为`width`传入，长边作为`height`传入，并设置`orientation: PageOrientation.LANDSCAPE`
- **绝不要使用`\
`** - 使用独立的Paragraph元素
- **绝不要使用Unicode项目符号** - 使用带编号配置的`LevelFormat.BULLET`
- **PageBreak必须放在Paragraph内** - 单独使用会生成无效XML
- **ImageRun需要指定`type`** - 始终明确指定png/jpg等类型
- **始终使用DXA设置表格`width`** - 绝不要使用`WidthType.PERCENTAGE`（在Google Docs中会失效）
- **表格需要双宽度设置** - `columnWidths`数组和单元格`width`必须匹配
- **表格宽度 = columnWidths的总和** - 对于DXA单位，确保数值完全相加匹配
- **始终添加单元格边距** - 使用`margins: { top: 80, bottom: 80, left: 120, right: 120 }`以获得易读的内边距
- **使用`ShadingType.CLEAR`** - 表格底纹绝不要使用SOLID
- **目录仅支持HeadingLevel** - 标题段落不要使用自定义样式
- **覆盖内置样式** - 使用精确的ID："Heading1"、"Heading2"等
- **包含`outlineLevel`** - 目录需要此属性（H1对应0，H2对应1等）

---

## 编辑现有文档

**按顺序执行以下3个步骤。**

### 步骤1：解压
```bash
python scripts/office/unpack.py document.docx unpacked/
```
提取XML文件、格式化输出、合并相邻的run，并将智能引号转换为XML实体（如`&#x201C;`等），以确保编辑后不会丢失。使用`--merge-runs false`可跳过run合并。

### 步骤2：编辑XML

编辑`unpacked/word/`目录下的文件。请参考下方的XML参考获取格式示例。

**将“Claude”作为作者**用于修订内容和批注，除非用户明确要求使用其他名称。

**直接使用编辑工具进行字符串替换。不要编写Python脚本。** 脚本会引入不必要的复杂性。编辑工具可直观显示替换内容。

**关键注意事项：为新内容使用智能引号。** 添加带有撇号或引号的文本时，使用XML实体来生成智能引号：
```xml
<!-- 使用这些实体来实现专业排版 -->
<w:t>Here&#x2019;s a quote: &#x201C;Hello&#x201D;</w:t>
```
| 实体 | 字符 |
|--------|-----------|
| `&#x2018;` | ‘ (左单引号) |
| `&#x2019;` | ’ (右单引号/撇号) |
| `&#x201C;` | “ (左双引号) |
| `&#x201D;` | ” (右双引号) |

**添加批注：** 使用`comment.py`处理多个XML文件中的重复代码（文本必须是预转义的XML）：
```bash
python scripts/comment.py unpacked/ 0 "批注文本，包含&amp;和&#x2019;"
python scripts/comment.py unpacked/ 1 "回复文本" --parent 0  # 回复批注0
python scripts/comment.py unpacked/ 0 "文本" --author "自定义作者"  # 自定义作者名称
```
然后在document.xml中添加标记（请参考XML参考中的批注部分）。

### 步骤3：重新打包
```bash
python scripts/office/pack.py unpacked/ output.docx --original document.docx
```
执行自动修复验证、压缩XML并生成DOCX文件。使用`--validate false`可跳过验证。

**自动修复可解决以下问题：**
- `durableId` >= 0x7FFFFFFF（重新生成有效的ID）
- 带有空格的`<w:t>`元素缺少`xml:space="preserve"`属性

**自动修复无法解决以下问题：**
- 格式错误的XML、无效的元素嵌套、缺失的关联关系、Schema违规

### 常见陷阱

- **替换整个`<w:r>`元素**：添加修订内容时，将整个`<w:r>...</w:r>`块替换为同级的`<w:del>...<w:ins>...`。不要在run内部注入修订标记。
- **保留`<w:rPr>`格式**：将原始run的`<w:rPr>`块复制到修订内容的run中，以保持加粗、字号等格式。

---

## XML参考

### Schema合规性

- `<w:pPr>`中的元素顺序：`<w:pStyle>`、`<w:numPr>`、`<w:spacing>`、`<w:ind>`、`<w:jc>`、`<w:rPr>`（最后）
- **空格处理**：对带有前导/尾随空格的`<w:t>`元素添加`xml:space="preserve"`属性
- **RSIDs**：必须是8位十六进制数（例如`00AB1234`）

### 修订内容

**插入内容：**
```xml
<w:ins w:id="1" w:author="Claude" w:date="2025-01-01T00:00:00Z">
  <w:r><w:t>插入的文本</w:t></w:r>
</w:ins>
```

**删除内容：**
```xml
<w:del w:id="2" w:author="Claude" w:date="2025-01-01T00:00:00Z">
  <w:r><w:delText>删除的文本</w:delText></w:
</w:del>
```

**在`<w:del>`内部：** 使用`<w:delText>`代替`<w:t>`，使用`<w:delInstrText>`代替`<w:instrText>`。

**最小化编辑** - 仅标记更改的部分：
```xml
<!-- 将“30天”改为“60天” -->
<w:r><w:t>期限为 </w:t></w:r>
<w:del w:id="1" w:author="Claude" w:date="...">
  <w:r><w:delText>30</w:delText></w:
</w:del>
<w:ins w:id="2" w:author="Claude" w:date="...">
  <w:r><w:t>60</w:t></w:r>
</w:ins>
<w:r><w:t> 天。</w:t></w:r>
```

**删除整个段落/列表项** - 删除段落的所有内容时，还需将段落标记标记为已删除，使其与下一段落合并。在`<w:pPr><w:rPr>`中添加`<w:del/>`：
```xml
<w:p>
  <w:pPr>
    <w:numPr>...</w:numPr>  <!-- 若存在则保留列表编号 -->
    <w:rPr>
      <w:del w:id="1" w:author="Claude" w:date="2025-01-01T00:00:00Z"/>
    </w:rPr>
  </w:pPr>
  <w:del w:id="2" w:author="Claude" w:date="2025-01-01T00:00:00Z">
    <w:r><w:delText>要删除的整个段落内容...</w:delText></w:r>
  </w:del>
</w:p>
```
如果不在`<w:pPr><w:rPr>`中添加`<w:del/>`，接受修订后会留下空段落/列表项。

**拒绝其他作者的插入内容** - 在他们的插入内容中嵌套删除标记：
```xml
<w:ins w:author="Jane" w:id="5">
  <w:del w:author="Claude" w:id="10">
    <w:r><w:delText>他们插入的文本</w:delText></w:r>
  </w:del>
</w:ins>
```

**恢复其他作者删除的内容** - 在删除内容后添加插入标记（不要修改他们的删除标记）：
```xml
<w:del w:author="Jane" w:id="5">
  <w:r><w:delText>被删除的文本</w:delText></w:r>
</w:del>
<w:ins w:author="Claude" w:id="10">
  <w:r><w:t>被删除的文本</w:t></w:r>
</w:ins>
```

### 批注

运行`comment.py`后（请参考步骤2），在document.xml中添加标记。如果是回复，请使用`--parent`标志并将标记嵌套在父批注的标记内。

**关键注意事项：`<w:commentRangeStart>`和`<w:commentRangeEnd>`是`<w:r>`的同级元素，绝不要放在`<w:r>`内部。**

```xml
<!-- 批注标记是w:p的直接子元素，绝不要放在w:r内部 -->
<w:commentRangeStart w:id="0"/>
<w:del w:id="1" w:author="Claude" w:date="2025-01-01T00:00:00Z">
  <w:r><w:delText>已删除</w:delText></w:r>
</w:del>
<w:r><w:t> 更多文本</w:t></w:r>
<w:commentRangeEnd w:id="0"/>
<w:r><w:rPr><w:rStyle w:val="CommentReference"/></w:rPr><w:commentReference w:id="0"/></w:r>

<!-- 批注0嵌套回复1 -->
<w:commentRangeStart w:id="0"/>
  <w:commentRangeStart w:id="1"/>
  <w:r><w:t>文本</w:t></w:r>
  <w:commentRangeEnd w:id="1"/>
<w:commentRangeEnd w:id="0"/>
<w:r><w:rPr><w:rStyle w:val="CommentReference"/></w:rPr><w:commentReference w:id="0"/></w:r>
<w:r><w:rPr><w:rStyle w:val="CommentReference"/></w:rPr><w:commentReference w:id="1"/></w:
```

### 图片

1. 将图片文件添加到`word/media/`目录
2. 在`word/_rels/document.xml.rels`中添加关联关系：
```xml
<Relationship Id="rId5" Type=".../image" Target="media/image1.png"/>
```
3. 在`[Content_Types].xml`中添加内容类型：
```xml
<Default Extension="png" ContentType="image/png"/>
```
4. 在document.xml中引用：
```xml
<w:drawing>
  <wp:inline>
    <wp:extent cx="914400" cy="914400"/>  <!-- EMUs单位：914400 = 1英寸 -->
    <a:graphic>
      <a:graphicData uri=".../picture">
        <pic:pic>
          <pic:blipFill><a:blip r:embed="rId5"/></pic:blipFill>
        </pic:pic>
      </a:graphicData>
    </a:graphic>
  </wp:inline>
</w:drawing>
```

---

## 依赖项

- **pandoc**：文本提取
- **docx**：`npm install -g docx`（用于创建新文档）
- **LibreOffice**：PDF转换（通过`scripts/office/soffice.py`在沙箱环境中自动配置）
- **Poppler**：用于图片转换的`pdftoppm`工具