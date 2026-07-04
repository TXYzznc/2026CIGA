using System.Text;

/// <summary>
/// 数字艺术字辅助：把数字转成 TMP 的 sprite 标签串。
/// TMP 文本组件里直接 text = NumText.ToSpriteTags(320) 即可显示手绘数字。
/// </summary>
public static class NumText
{
    private const string AssetName = "ZZNC_NumSpriteAsset";

    private static readonly StringBuilder Sb = new StringBuilder(64);

    /// <summary>整数 → sprite 标签串（如 320 → 三个标签）。</summary>
    public static string ToSpriteTags(int value)
    {
        return ToSpriteTags(value.ToString());
    }

    /// <summary>数字字符串 → sprite 标签串。非数字字符原样保留（如 "/"、"×"）。</summary>
    public static string ToSpriteTags(string text)
    {
        Sb.Clear();
        foreach (char c in text)
        {
            if (c >= '0' && c <= '9')
                Sb.Append("<sprite=\"").Append(AssetName).Append("\" name=\"").Append(c).Append("\">");
            else
                Sb.Append(c);
        }
        return Sb.ToString();
    }
}
