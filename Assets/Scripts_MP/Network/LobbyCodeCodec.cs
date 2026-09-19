using System;
using System.Text.RegularExpressions;

/// <summary>
/// Edgegap relay session_id ('feedback: 5d028b6d8c78-S' = 12 hex, 48 bit) ile
/// insan-okunur kod (A-Z + 2-9, hiçbir 6/1/I/L/O belirsizlik küçük uni) arasında
/// tam (bijective) dönüşüm — Edgegap servisi gerekmez, offline çalışır.
///
/// 10 hane: 31^10 ≈ 8.2e14 > 2^48 ≈ 2.8e14 → tüm olası id'ler tekil kodlanır.
/// 6 hane:  31^6  ≈ 8.9e8  < 2^48 → matematiksel olarak imkânsız (bu yüzden 10).
/// </summary>
public static class LobbyCodeCodec
{
    public const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    public const int CodeLength = 10;
    public const int ShortLength = 6;

    private const ulong ShortModulus = 887503681UL;   // 31^6

    /// <summary>
    /// 6 haneli kısa kod — session_id'nin 48 bitlik değerinin 31^6 modülü.
    /// Tek başına geri çevrilemez (30 bit), Join tarafında aktif session listesiyle
    /// eşleştirilir. Çakışma olasılığı ~ k / 8.9e8 (k = eşzamanlı oturum sayısı).
    /// </summary>
    public static string Code6(string sessionId)
    {
        ulong bits = ParseSessionBits(sessionId);
        ulong v = bits % ShortModulus;

        var sb = new char[ShortLength];
        for (int i = 0; i < ShortLength; i++)
        {
            sb[i] = Alphabet[(int)(v % 31UL)];
            v /= 31UL;
        }
        return new string(sb);
    }

    /// <summary>Kullanıcının girdiği 6 haneli kod geçerli alfabede mi?</summary>
    public static bool IsValidShortCode(string code)
    {
        if (string.IsNullOrEmpty(code) || code.Length != ShortLength) return false;
        foreach (char c in code.ToUpperInvariant())
            if (Alphabet.IndexOf(c) < 0) return false;
        return true;
    }

    private static ulong ParseSessionBits(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId))
            throw new ArgumentException("session id boş");

        string hexPart = sessionId.Split('-')[0].ToLowerInvariant();
        if (hexPart.Length != 12 || !IsHex(hexPart))
            throw new FormatException("Beklenmedik Edgegap session_id biçimi: " + sessionId);

        return Convert.ToUInt64(hexPart, 16);
    }

    /// <summary>'5d028b6d8c78-S' -> 'XY7QZB9R2Z' (örnek biçim)</summary>
    public static string Encode(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId))
            throw new ArgumentException("session id boş");

        string hexPart = sessionId.Split('-')[0].ToLowerInvariant();
        if (hexPart.Length != 12 || !IsHex(hexPart))
            throw new FormatException("Beklenmedik Edgegap session_id biçimi: " + sessionId);

        ulong bits = ParseSessionBits(sessionId); // 12 hex = 48 bit

        var sb = new char[CodeLength];
        for (int i = 0; i < CodeLength; i++)
        {
            int index = (int)(bits % 31UL);
            sb[i] = Alphabet[index];
            bits /= 31UL;
        }
        if (bits != 0) throw new OverflowException("session id 48 bitten büyük");
        return new string(sb);
    }

    /// <summary>Encode'in tam tersi; hatalı kod throws.</summary>
    public static string Decode(string code)
    {
        if (string.IsNullOrEmpty(code) || code.Length != CodeLength)
            throw new FormatException("Kod 10 hanedan olmalı.");

        ulong bits = 0;
        for (int i = CodeLength - 1; i >= 0; i--)
        {
            int idx = Alphabet.IndexOf(code[i]);
            if (idx < 0) throw new FormatException("Geçersiz karakter: " + code[i]);
            bits = bits * 31 + (ulong)idx;
        }

        return bits.ToString("x12") + "-S";
    }

    /// <summary>Girdi bir codec kodu mu? (Join input doğrulama için)</summary>
    public static bool TryDecode(string code, out string sessionId)
    {
        try { sessionId = Decode(code); return true; }
        catch { sessionId = null; return false; }
    }

    private static bool IsHex(string s)
    {
        foreach (var c in s)
            if (!(char.IsDigit(c) || (c >= 'a' && c <= 'f')))
                return false;
        return true;
    }
}
