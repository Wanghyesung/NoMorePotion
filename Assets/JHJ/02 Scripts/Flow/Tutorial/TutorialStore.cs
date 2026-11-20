using UnityEngine;

public class TutorialStore
{
    private const string PROMPTED_KEY = "TutorialPrompted";

    public bool HasPrompted => PlayerPrefs.GetInt(PROMPTED_KEY, 0) == 1;

    public void MarkPrompted()
    {
        PlayerPrefs.SetInt(PROMPTED_KEY, 1);
        PlayerPrefs.Save();
    }
}
