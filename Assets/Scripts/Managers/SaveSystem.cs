using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class SaveData
{
    // Level indexes match GameSceneManager.scenes (0 = Menu, 1 = Tutorial, 2 = Demo ...).
    public int highestUnlockedLevel = 1;
    public List<int> completedLevels = new List<int>();
}

// Static save file access. Stored as JSON under Application.persistentDataPath.
public static class SaveSystem
{
    private const string FileName = "save.json";
    private const int FirstLevel = 1;

    private static SaveData data;

    private static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    public static SaveData Data
    {
        get
        {
            if (data == null) Load();
            return data;
        }
    }

    public static bool HasSave()
    {
        return File.Exists(FilePath);
    }

    public static void Load()
    {
        data = null;

        if (HasSave())
        {
            try
            {
                data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"SaveSystem: Could not read save file, starting fresh. {e.Message}");
            }
        }

        if (data == null) data = new SaveData();
        if (data.completedLevels == null) data.completedLevels = new List<int>();
        if (data.highestUnlockedLevel < FirstLevel) data.highestUnlockedLevel = FirstLevel;
    }

    public static void Save()
    {
        try
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(Data, true));
        }
        catch (Exception e)
        {
            Debug.LogError($"SaveSystem: Could not write save file. {e.Message}");
        }
    }

    // Wipes progress and writes a fresh save (used by New Game).
    public static void ResetProgress()
    {
        data = new SaveData();
        Save();
    }

    public static bool IsLevelUnlocked(int levelIndex)
    {
        return levelIndex >= FirstLevel && levelIndex <= Data.highestUnlockedLevel;
    }

    public static bool IsLevelCompleted(int levelIndex)
    {
        return Data.completedLevels.Contains(levelIndex);
    }

    // Marks the level as completed and unlocks the next one.
    public static void CompleteLevel(int levelIndex)
    {
        if (levelIndex < FirstLevel) return;

        if (!Data.completedLevels.Contains(levelIndex)) Data.completedLevels.Add(levelIndex);
        if (levelIndex + 1 > Data.highestUnlockedLevel) Data.highestUnlockedLevel = levelIndex + 1;

        Save();
    }
}
