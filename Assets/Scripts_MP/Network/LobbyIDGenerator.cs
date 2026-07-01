// ============================================================
// File:    LobbyIDGenerator.cs
// Author:  Murad
// Created: 30-Jun-2026
// Purpose: Utility methods for generating unique lobby IDs
// ============================================================

using System.Text;
using UnityEngine;

public static class LobbyIDGenerator
{
    // Randomly generates a six-character lobby code
    const string Chars = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public static string Generate(int length = 6)
    {
        var sb = new StringBuilder(length);
        for (int i = 0; i < length; i++)
            sb.Append(Chars[Random.Range(0, Chars.Length)]);
        return sb.ToString();
    }
}