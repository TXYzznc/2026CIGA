/// <summary>
/// 工具箱面板接口 - 所有集成到工具箱的工具都需要实现此接口
/// </summary>
public interface IToolHubPanel
{
    /// <summary>工具被激活时调用</summary>
    void OnEnable();
    
    /// <summary>工具被隐藏时调用</summary>
    void OnDisable();
    
    /// <summary>绘制工具界面</summary>
    void OnGUI();
    
    /// <summary>工具被销毁时调用</summary>
    void OnDestroy();
    
    /// <summary>获取工具的帮助提示（可选）</summary>
    string GetHelpText() => string.Empty;
}
