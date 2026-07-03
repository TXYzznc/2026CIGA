using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 工具箱配置 - 持久化存储工具箱的状态
/// </summary>
public class ToolHubSettings : ScriptableObject
{
    /// <summary>是否锁定（防止误操作）</summary>
    public bool locked;
    
    /// <summary>当前选中的标签页索引</summary>
    public int selectedIndex;
    
    /// <summary>已添加的工具列表</summary>
    public List<ToolEntry> tools = new();

    [Serializable]
    public class ToolEntry
    {
        /// <summary>工具类型的完全限定名</summary>
        public string typeName;
        
        /// <summary>标签页显示名称（可自定义）</summary>
        public string displayName;
        
        /// <summary>工具描述</summary>
        public string description;
    }
}
