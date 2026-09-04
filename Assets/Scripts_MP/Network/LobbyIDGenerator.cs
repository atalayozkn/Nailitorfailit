
using System.Text;
using UnityEngine;

public static class LobbyIDGenerator
{

    const string Chars = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public static string Generate(int length = 6)
    {
        var sb = new StringBuilder(length);
        for (int i = 0; i < length; i++)
            sb.Append(Chars[Random.Range(0, Chars.Length)]);
        return sb.ToString();
    }
}
