using UnityEngine;

/// <summary>
/// Lobi kodu: kullaniciya gosterilen, arkadaslarin yazarak katildigi 6 karakterlik
/// kod. Alfabede 0/1/I/L/O yok — sesli okundugunda veya elle yazildiginda karismaz.
///
/// Kod artik rastgele uretilir ve Steam lobby metadata'sindaki "code" anahtarina
/// yazilir; katilan taraf SteamMatchmaking.RequestLobbyList ile string filtre
/// uygulayarak bu kodu arar (Edgegap'teki session_id turetmesi kaldirildi).
///
/// Cakisma olasiligi: 31^6 ≈ 8.9e8. Ayni anda k lobi varken P(cakisma) ≈ k²/(2·8.9e8)
/// — k=100 icin ~5.6e-6. RequestLobbyList birden fazla sonuc dondururse
/// SteamLobbyManager uyarip ilkini kullanir.
/// </summary>
public static class LobbyCodeCodec
{
    public const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    public const int ShortLength = 6;

    /// <summary>Rastgele 6 karakterlik lobi kodu uretir.</summary>
    public static string RandomCode()
    {
        char[] chars = new char[ShortLength];
        for (int i = 0; i < ShortLength; i++)
            chars[i] = Alphabet[Random.Range(0, Alphabet.Length)];
        return new string(chars);
    }

    /// <summary>Kullanıcının girdiği 6 haneli kod geçerli alfabede mi?</summary>
    public static bool IsValidShortCode(string code)
    {
        if (string.IsNullOrEmpty(code) || code.Length != ShortLength) return false;
        foreach (char c in code.ToUpperInvariant())
            if (Alphabet.IndexOf(c) < 0) return false;
        return true;
    }

    /// <summary>Girdiyi karsilastirilabilir hale getirir (bosluk temizligi + buyuk harf).</summary>
    public static string Canonicalize(string input) =>
        string.IsNullOrEmpty(input) ? string.Empty : input.Trim().ToUpperInvariant();
}
