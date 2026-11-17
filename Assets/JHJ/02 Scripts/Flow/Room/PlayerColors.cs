using UnityEngine;

public static class PlayerColors
{
    public static readonly Color[] Palette =
    {
        Hex("#FF8336"),  // 기본값
        Hex("#FF9B92"),  // 빨
        Hex("#FF92CB"),  // 핑
        Hex("#E992FF"),  // 보
        Hex("#92ACFF"),  // 파
        Hex("#92D3FF"),  // 하늘
        Hex("#92FFE3"),  // 민트
        Hex("#92FFAC"),  // 초
        Hex("#F6FF92")   // 노란
    };

    public static Color Get(int index)
    {
        return index >= 0 && index < Palette.Length ? Palette[index] : Palette[0];
    }

    private static Color Hex(string hex)
    {
        return ColorUtility.TryParseHtmlString(hex, out Color c) ? c : Color.white;
    }
}
