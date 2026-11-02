using UnityEngine;

public static class RoomCodeGenerator 
{
    private const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private const int Length = 6;


    public static string Generate()
    {
        char[] code = new char[Length];

        for (int i = 0; i < Length; i++)
        {
            int index = Random.Range(0, Chars.Length);
            code[i] = Chars[index];
        }

        return new string(code);
    }
}
