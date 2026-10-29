using UnityEngine;

public static class LobbyCodeCodec
{
    public const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    public const int ShortLength = 6;

    public static string RandomCode()
    {
        char[] chars = new char[ShortLength];
        for (int i = 0; i < ShortLength; i++)
            chars[i] = Alphabet[Random.Range(0, Alphabet.Length)];
        return new string(chars);
    }

    public static bool IsValidShortCode(string code)
    {
        if (string.IsNullOrEmpty(code) || code.Length != ShortLength) return false;
        foreach (char c in code.ToUpperInvariant())
            if (Alphabet.IndexOf(c) < 0) return false;
        return true;
    }

    public static string Canonicalize(string input) =>
        string.IsNullOrEmpty(input) ? string.Empty : input.Trim().ToUpperInvariant();
}
