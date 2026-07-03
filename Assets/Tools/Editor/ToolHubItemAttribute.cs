using System;

/// <summary>
/// 工具箱项目特性 - 标记可以被工具箱发现的工具面板
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class ToolHubItemAttribute : Attribute
{
    /// <summary>在添加菜单中显示的名称（支持路径格式如 "纹理工具/噪声生成器"）</summary>
    public readonly string MenuName;
    
    /// <summary>工具的简短描述</summary>
    public readonly string Description;
    
    /// <summary>排序优先级（数值越小越靠前）</summary>
    public readonly int Priority;

    public ToolHubItemAttribute(string menuName, string description = "", int priority = 100)
    {
        MenuName = menuName;
        Description = description;
        Priority = priority;
    }
}
