using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

//technically, it's more efficient to store the data as raw binary instead of string data. this method was simpler for me to program though
public static class SaveManager
{
    public static readonly bool debug = false;
    public const bool DefaultSaveOnChange = false;

    //list containing the name of the save files
    public static List<string> SaveFiles()
    {
        List<string> temp = new();

        foreach (SaveFile file in saveFiles)
            temp.Add(file.SavedName);

        return temp;
    }

    //save data for each file
    private static List<SaveFile> saveFiles = new();

    //the main, and default, save file
    private static SaveFile mainFile;

    //other save files should probably be manually added here.

    public static SaveFile MainSave()
    {
        // create default file if it doesn't exist
        if (mainFile == null)
        {
            mainFile = new SaveFile("MainSave");
        }
        return mainFile;
    }

    /// <summary>
    /// Lookup savefile by path
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public static SaveFile GetFile(string path)
    {
        return saveFiles.Find(file => file.SavedPath == path);
    }

    //register a new savefile
    internal static void RegisterFile(SaveFile file)
    {
        if (saveFiles.Find(saveFile => saveFile.SavedPath == file.SavedPath) == null)
            saveFiles.Add(file);
    }

    //call some other way if used elsewhere
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    public static void Initialize()
    {
        Application.quitting += OnQuit;

        if (!Directory.Exists(Globals.SaveFolderPath))
            Directory.CreateDirectory(Globals.SaveFolderPath);
    }
    
    //[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void LoadAll()
    {
        foreach (SaveFile saveFile in saveFiles)
        {
            saveFile.Load();
        }
    }
    
    public static void SaveAll()
    {
        foreach (SaveFile saveFile in saveFiles)
        {
            saveFile.Save();
        }
    }

    private static void OnQuit()
    {
        SaveAll();
    }
}

/// <summary>
/// Represents a single save file (text-based).
/// </summary>
public class SaveFile
{
    public string SavedName { get; }
    public string SavedPath { get; }
    public string FileEnding { get; }

    public bool SaveOnChange { get; set; } = SaveManager.DefaultSaveOnChange;

    /// <summary>
    /// The file as it was written last time it was read.
    /// </summary>
    protected Dictionary<string, string> values = new();

    public IReadOnlyDictionary<string, string> Values => values;

    public event Action<string> OnValueChanged;

    public SaveFile(string fileName, string fileEnding = ".txt", string savePath = null, bool createIfNotFound = true)
    {
        if (fileName == null)
            throw new ArgumentNullException(nameof(fileName));

        SavedName = fileName + fileEnding;
        SavedPath = savePath ?? Path.Combine(Globals.SaveFolderPath, SavedName);
        FileEnding = fileEnding;

        if (!File.Exists(SavedPath))
        {
            Debug.LogWarning($"{fileName} does not exist");

            if (createIfNotFound)
            {
                File.CreateText(SavedPath).Dispose();
                Save();
                Debug.Log($"creating {fileName}");
            }
        }

        SaveManager.RegisterFile(this);

        /*
        using FileSystemWatcher watcher = new FileSystemWatcher(Globals.SaveFolderPath);

        watcher.Changed += (object sender, FileSystemEventArgs e) => WriteVariables();

        watcher.Filter = $"{fileName}";
        watcher.EnableRaisingEvents = true;

        Application.quitting += () => watcher.Dispose();
        */
    }


    /// <summary>
    /// Saves the everything stored to associated file
    /// </summary>
    public virtual void Save(bool updateCacheAfter = true)
    {
        try
        {
            using StreamWriter sw = new StreamWriter(SavedPath, false);

            foreach (var pair in values)
            {
                sw.WriteLine($"{pair.Key}={pair.Value}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"[SaveFile] Failed to save {SavedName}: {ex}");
        }

        if (updateCacheAfter)
            UpdateCache();
    }

    /// <summary>
    /// Updates the SaveVariables to match the file
    /// </summary>
    public virtual Dictionary<string, string> Load()
    {
        UpdateCache();
        return values;
    }

    public List<string> cachedText = new();

    /// <summary>
    /// Updates the cachedText
    /// </summary>
    internal void UpdateCache()
    {
        try
        {
            using StreamReader sr = new StreamReader(SavedPath);

            values.Clear();

            string line;

            while ((line = sr.ReadLine()) != null)
            {
                cachedText.Add(line);

                int separator = line.IndexOf('=');

                if (separator <= 0)
                    continue;

                string key = line.Substring(0, separator);
                string value = line.Substring(separator + 1);

                values[key] = value;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveFile] Failed to read {SavedName}: {ex}");
        }
    }

    public bool HasValue(string key)
    {
        return values.ContainsKey(key);
    }

    public string Get(string key, string defaultValue = null)
    {
        if (values.TryGetValue(key, out string value))
            return value;

        return defaultValue;
    }

    public void Set(string key, string value)
    {
        values[key] = value;

        OnValueChanged?.Invoke(key);

        if (SaveOnChange)
            Save();
    }

    #region Float
    public float GetFloat(string key, float defaultValue = 0f)
    {
        if (float.TryParse(Get(key), NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
        {
            return result;
        }

        return defaultValue;
    }

    public void SetFloat(string key, float value)
    {
        Set(key, value.ToString(CultureInfo.InvariantCulture));
    }
    #endregion

    #region Int
    public int GetInt(string key, int defaultValue = 0)
    {
        if (int.TryParse(Get(key), out int result))
            return result;

        return defaultValue;
    }

    public void SetInt(string key, int value)
    {
        Set(key, value.ToString());
    }
    #endregion

    #region Bool
    public bool GetBool(string key, bool defaultValue = false)
    {
        if (bool.TryParse(Get(key), out bool result))
            return result;

        return defaultValue;
    }

    public void SetBool(string key, bool value)
    {
        Set(key, value.ToString());
    }
    #endregion

    #region String
    public string GetString(string key, string defaultValue = "")
    {
        return Get(key, defaultValue) ?? defaultValue;
    }

    public void SetString(string key, string value)
    {
        Set(key, value ?? "");
    }
    #endregion

    #region Enum
    public T GetEnum<T>(string key, T defaultValue = default) where T : struct, Enum
    {
        if (Enum.TryParse(Get(key), true, out T result))
            return result;

        return defaultValue;
    }

    public void SetEnum<T>(string key, T value) where T : struct, Enum
    {
        Set(key, value.ToString());
    }
    #endregion

    #region Color
    public Color GetColor(string key, Color defaultValue = default)
    {
        string value = Get(key);

        if (string.IsNullOrEmpty(value))
            return defaultValue;

        string[] parts = value.Split(',');

        if (parts.Length != 4)
            return defaultValue;

        if (!float.TryParse(parts[0], NumberStyles.Float,
                CultureInfo.InvariantCulture, out float r))
            return defaultValue;

        if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float g))
            return defaultValue;

        if (!float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float b))
            return defaultValue;

        if (!float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float a))
            return defaultValue;

        return new Color(r, g, b, a);
    }

    public void SetColor(string key, Color value)
    {
        string serialized = string.Join(",", value.r.ToString(CultureInfo.InvariantCulture), value.g.ToString(CultureInfo.InvariantCulture), value.b.ToString(CultureInfo.InvariantCulture), value.a.ToString(CultureInfo.InvariantCulture));

        Set(key, serialized);
    }
    #endregion
}

/// <summary>
/// Base class for any variable saved in a SaveFile.
/// </summary>
public abstract class SaveVariable
{
    public string SavedName { get; }
    public SaveFile SaveFile { get; }

    protected SaveVariable(string savedName, SaveFile saveFile)
    {
        SavedName = savedName ?? throw new ArgumentNullException(nameof(savedName));

        SaveFile = saveFile
            ?? throw new ArgumentNullException(nameof(saveFile));
    }

    public abstract object GetAsObject();
    public abstract void SetFromObject(object value);

    public abstract string GetAsString();
    public abstract void SetFromString(string value);
}


/// <summary>
/// Stores and retrieves float values
/// </summary>
public class SaveFloat : SaveVariable
{
    public static implicit operator float(SaveFloat obj) => obj.Get();

    public SaveFloat(string savedName, SaveFile saveFile = null, float defaultValue = 0f) : base(savedName, saveFile ?? SaveManager.MainSave())
    {
        if (!SaveFile.HasValue(SavedName))
            SaveFile.SetFloat(SavedName, defaultValue);
    }

    public float Get() => SaveFile.GetFloat(SavedName);
    

    public void Set(float value) => SaveFile.SetFloat(SavedName, value);

    public override object GetAsObject() => Get();
    public override void SetFromObject(object value) => Set((float)value);

    public override string GetAsString() => Get().ToString(CultureInfo.InvariantCulture);
    public override void SetFromString(string value)
    {
        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
            Set(result);
    }
}

/// <summary>
/// Stores and retrieves int values
/// </summary>
public class SaveInt : SaveVariable
{
    public static implicit operator int(SaveInt obj) => obj.Get();

    public SaveInt(string savedName, SaveFile saveFile = null, int defaultValue = 0) : base(savedName, saveFile ?? SaveManager.MainSave()) { }

    public int Get() => SaveFile.GetInt(SavedName);

    public void Set(int value) => SaveFile.SetInt(SavedName, value);

    public override object GetAsObject() => Get();
    public override void SetFromObject(object value) => Set((int)value);

    public override string GetAsString() => Get().ToString(CultureInfo.InvariantCulture);
    public override void SetFromString(string value)
    {
        if (int.TryParse(value, out int result))
            Set(result);
    }
}

/// <summary>
/// Stores and retrieves bool values
/// </summary>
public class SaveBool : SaveVariable
{
    public static implicit operator bool(SaveBool obj) => obj.Get();

    public SaveBool(string savedName, SaveFile saveFile = null, bool defaultValue = false) : base(savedName, saveFile ?? SaveManager.MainSave())
    {
        if (!SaveFile.HasValue(SavedName))
            SaveFile.SetBool(SavedName, defaultValue);
    }

    public bool Get() => SaveFile.GetBool(SavedName);

    public void Set(bool value) => SaveFile.SetBool(SavedName, value);

    public override object GetAsObject() => Get();
    public override void SetFromObject(object value) => Set((bool)value);

    public override string GetAsString() => Get().ToString(CultureInfo.InvariantCulture);
    public override void SetFromString(string value)
    {
        if (bool.TryParse(value, out bool result))
            Set(result);
    }
}

/// <summary>
/// Stores and retrieves string values
/// </summary>
public class SaveString : SaveVariable
{
    public static implicit operator string(SaveString obj) => obj.Get();

    public SaveString(string savedName, SaveFile saveFile = null, string defaultValue = "") : base(savedName, saveFile ?? SaveManager.MainSave())
    {
        if (!SaveFile.HasValue(SavedName))
            SaveFile.SetString(SavedName, defaultValue);
    }

    public string Get() => SaveFile.GetString(SavedName);

    public void Set(string value) => SaveFile.SetString(SavedName, value);

    public override object GetAsObject() => Get();
    public override void SetFromObject(object value) => Set((string)value);

    public override string GetAsString() => Get();
    public override void SetFromString(string value) => Set(value);
}

/// <summary>
/// Stores and retrieves Enum values
/// </summary>
public class SaveEnum<T> : SaveVariable where T : struct, Enum
{
    public static implicit operator T(SaveEnum<T> obj) => obj.Get();

    public SaveEnum(string savedName, SaveFile saveFile = null, T defaultValue = default) : base(savedName, saveFile ?? SaveManager.MainSave()) { }

    public T Get() => SaveFile.GetEnum<T>(SavedName);

    public void Set(T value) => SaveFile.SetEnum(SavedName, value);

    public override object GetAsObject() => Get();
    public override void SetFromObject(object value) => Set((T)value);

    public override string GetAsString() => Get().ToString();
    public override void SetFromString(string v)
    {
        if (Enum.TryParse(v, true, out T result))
            Set(result);
    }
}

/// <summary>
/// Stores and retrieves Color values
/// </summary>
public class SaveColor : SaveVariable
{
    public static implicit operator Color(SaveColor obj) => obj.Get();

    public SaveColor(string savedName, SaveFile saveFile = null, Color defaultValue = default) : base(savedName, saveFile) { }

    public Color Get(Color defaultValue = default) => SaveFile.GetColor(SavedName, defaultValue);

    public void Set(Color value) => SaveFile.SetColor(SavedName, value);

    public void SetRed(float r)
    {
        Set(new Color(r, Get().g, Get().b));
    }

    public void SetGreen(float g)
    {
        Set(new Color(Get().r, g, Get().b));
    }

    public void SetBlue(float b)
    {
        Set(new Color(Get().r, Get().g, b));
    }

    public override object GetAsObject() => Get();
    public override void SetFromObject(object v) => Set((Color)v);

    public override string GetAsString() => Get().ToString();
    public override void SetFromString(string v)
    {
        if (TryParseColor(v, out Color color))
            Set(color);
    }

    private static bool TryParseColor(string value, out Color color)
    {
        color = Color.black;

        if (string.IsNullOrEmpty(value))
            return false;

        string[] colors = value.Split(',');

        if (colors.Length != 4)
            return false;

        if (!float.TryParse(colors[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float r) ||
            !float.TryParse(colors[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float g) ||
            !float.TryParse(colors[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float b) ||
            !float.TryParse(colors[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float a))
        {
            return false;
        }

        color = new Color(r, g, b, a);
        return true;
    }
}