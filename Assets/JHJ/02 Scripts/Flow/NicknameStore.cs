using UnityEngine;

public class NicknameStore
{
    private const string NICKNAME_KEY = "Nickname";
    public const int MAX_LENGTH = 10;     // [임시] 글자수 제한(닉네임), 현재 기본 닉네임이 딱 10자

    public string Load()
    {
        return PlayerPrefs.GetString(NICKNAME_KEY, "");
    }

    public void Save(string name)
    {
        PlayerPrefs.SetString(NICKNAME_KEY, name);
        PlayerPrefs.Save();
    }

    public string CreateDefault()
    {
        return "Player" + Random.Range(1000, 10000);
    }
}
