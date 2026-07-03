# DataTable — 配置表

> 配置表源文件（JSON 格式），同时也是运行时加载的资源。

## 工作流

1. 在本目录下创建/编辑 `{TableName}.json`，文件内同时包含表名(`table`)、字段定义(`fields`)、数据行(`rows`)
2. 在 Unity 菜单 `Tools/DataTable/生成全部配置表代码`，生成对应的 `.cs` 文件
3. 生成的 `{TableName}Row` / `{TableName}` 类位于 `Assets/Scripts/DataTable/`，**不要手改**

## JSON 格式示例

```json
{
  "table": "ItemTable",
  "fields": [
    { "name": "Id",     "type": "int",    "desc": "唯一标识，主键" },
    { "name": "Name",   "type": "string", "desc": "物品名称" }
  ],
  "rows": [
    { "Id": 1001, "Name": "治疗药水" }
  ]
}
```

- `table` 必须与文件名（不含扩展名）一致
- `fields[0]` 必须是 `{ "name": "Id", "type": "int" }`
- `type` 支持：`int` / `float` / `double` / `string` / `bool` 及对应数组类型（如 `int[]`）

## 运行时查询

```csharp
var item = GetModule<DataTableModule>().GetTable<ItemTable>().GetById(1001);
```
