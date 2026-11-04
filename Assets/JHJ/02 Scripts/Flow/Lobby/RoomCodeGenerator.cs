using UnityEngine;

public static class RoomCodeGenerator 
{
    private const string CHARS = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    public const int CODE_LENGTH = 6;


    public static string Generate()
    {
        char[] code = new char[CODE_LENGTH];

        for (int i = 0; i < CODE_LENGTH; i++)
        {
            int index = Random.Range(0, CHARS.Length);
            code[i] = CHARS[index];
        }

        return new string(code);
    }
}
