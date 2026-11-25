using UnityEngine;

/// <summary>
/// 미니맵 표시 설정
/// </summary>
public static class MinimapSettings
{
    private const string ROTATE_KEY = "MinimapRotate";
    private static bool? rotateWithPlayer;

    public static bool RotateWithPlayer
    {
        get
        {
            rotateWithPlayer ??= PlayerPrefs.GetInt(ROTATE_KEY, 1) == 1;
            return rotateWithPlayer.Value;
        }
        set
        {
            rotateWithPlayer = value;
        }
    }

    public static void Save()
    {
        PlayerPrefs.SetInt(ROTATE_KEY, RotateWithPlayer ? 1 : 0);
        PlayerPrefs.Save();
    }
}
