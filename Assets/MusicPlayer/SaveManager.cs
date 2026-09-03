using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

//technically, it's more efficient to store the data as raw binary instead of string data. this method was simpler for me to program though
public static class SaveManager
{
    public static readonly bool debug = false;

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
            mainFile = new SaveFile("MainSave.txt");
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
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Initialize()
    {
        if (!Directory.Exists(Globals.SaveFolderPath))
            Directory.CreateDirectory(Globals.SaveFolderPath);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void WriteAllVariables()
    {
        foreach (SaveFile saveFile in saveFiles)
        {
            saveFile.WriteVariables();
        }
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

    public List<SaveVariable> Variables { get; } = new();

    /// <summary>
    /// The file as it was written in the last read pass.
    /// </summary>
    public List<string> cachedText = new();

    public SaveFile(string fileName, string fileEnding = ".txt", string savePath = null, bool createIfNotFound = true)
    {
        if (fileName != null)
            SavedName = fileName + fileEnding;
        else
            throw new ArgumentNullException(nameof(fileName));

        SavedPath = savePath ?? Path.Combine(Globals.SaveFolderPath, fileName);

        if (!File.Exists(SavedPath))
        {
            Debug.LogWarning($"{fileName} does not exist");

            if (createIfNotFound)
            {
                File.CreateText(SavedPath);

                WriteFile();

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
    /// Updates the file to match the SaveVariables
    /// </summary>
    internal virtual void WriteFile(bool updateCacheAfter = true)
    {
        try
        {
            using StreamWriter sw = new StreamWriter(SavedPath, false);
            foreach (var variable in Variables.ToArray())
            {
                if (SaveManager.debug)
                    Debug.Log($"{variable.SavedName} updated in file to {variable.Value}");

                sw.WriteLine(variable.SavedString());
            }
        }
        catch (Exception ex)
        {
            Debug.Log($"[SaveFile] Failed to save {SavedName}: {ex}");
        }

        if (updateCacheAfter)
            UpdateCache();
    }

    /// <summary>
    /// Updates the SaveVariables to match the file
    /// </summary>
    public virtual List<string> WriteVariables()
    {
        UpdateCache();

        foreach (SaveVariable variable in Variables.ToArray())
        {
            foreach (string line in cachedText)
            {
                if (line.StartsWith($"{variable.SavedName}="))
                {
                    if (SaveManager.debug)
                        Debug.Log($"{variable.SavedName} updated from file to {variable.Value}");

                    variable.SetFromString(line.Remove(0, $"{variable.SavedName}=".Length), false);
                }
            }
        }

        return cachedText;
    }

    /// <summary>
    /// Updates the cachedText
    /// </summary>
    internal void UpdateCache()
    {
        try
        {
            using (StreamReader sr = new StreamReader(SavedPath, false))
            {
                cachedText.Clear();

                string line = sr.ReadLine();

                while (line != null)
                {
                    cachedText.Add(line);

                    line = sr.ReadLine();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.Log($"[SaveFile] Failed to read {SavedName}: {ex}");
        }
    }
}

/// <summary>
/// Base class for any variable saved in a SaveFile.
/// </summary>
public abstract class SaveVariable
{
    public string SavedName { get; internal set; }
    public SaveFile SaveFile { get; }
    private string _value = "";
    internal string Value
    {
        get => _value;
        set
        {
            _value = value;
        }
    }

    //maybe add events for when the file is updated?
    public event Action onSet;

    // enforce that all derived classes must implement Set and Get
    public abstract object GetAsObject();
    public abstract void SetFromObject(object v, bool UpdateOnChange = true);

    public abstract string GetAsString();
    public abstract void SetFromString(string v, bool UpdateOnChange = true);

    internal virtual string SavedString() => $"{SavedName}={Value}";

    protected SaveVariable(string savedName, SaveFile saveFile = null, object defaultValue = null)
    {
        SavedName = savedName ?? throw new ArgumentNullException(nameof(savedName));

        // decide which file this variable belongs to
        SaveFile = saveFile ?? SaveManager.MainSave();

        if (!SaveFile.Variables.Contains(this))
            SaveFile.Variables.Add(this);
        else
        {
            Debug.Log("unable to save this variable as it already exists in the save manager");
        }

        if(defaultValue != null)
        {
            SetFromObject(defaultValue, false);
        }
    }

    protected void OnSet()
    {
        if (SaveManager.debug)
            Debug.Log($"{SavedName} value set to: {Value}");

        onSet?.Invoke();
    }
}


/// <summary>
/// Stores and retrieves float values
/// </summary>
public class SaveFloat : SaveVariable
{
    public static implicit operator float(SaveFloat obj) => obj.Get();

    public SaveFloat(string savedName, SaveFile saveFile = null, float defaultValue = 0f) : base(savedName, saveFile, defaultValue) { }

    public float Get() => float.TryParse(Value, out var f) ? f : 0f;

    public void Set(float v, bool UpdateOnChange = true)
    {
        Value = v.ToString();
        OnSet();

        if (UpdateOnChange)
        {
            SaveFile.WriteFile();
        }
    }

    public override object GetAsObject() => Get();
    public override void SetFromObject(object v, bool UpdateOnChange = true) => Set((float)v, UpdateOnChange);

    public override string GetAsString() => Value;
    public override void SetFromString(string v, bool UpdateOnChange = true)
    {
        if (float.TryParse(v, out float result))
            Set(result, UpdateOnChange);
        else
            Value = default(float).ToString();
    }
}

/// <summary>
/// Stores and retrieves int values
/// </summary>
public class SaveInt : SaveVariable
{
    public static implicit operator int(SaveInt obj) => obj.Get();

    public SaveInt(string savedName, SaveFile saveFile = null, int defaultValue = 0) : base(savedName, saveFile, defaultValue) { }

    public int Get() => int.TryParse(Value, out var i) ? i : 0;

    public void Set(int v, bool UpdateOnChange = true)
    {
        Value = v.ToString();
        OnSet();

        if (UpdateOnChange)
        {
            SaveFile.WriteFile();
        }
    }

    public override object GetAsObject() => Get();
    public override void SetFromObject(object v, bool UpdateOnChange = true) => Set((int)v, UpdateOnChange);

    public override string GetAsString() => Value;
    public override void SetFromString(string v, bool UpdateOnChange = true)
    {
        if (int.TryParse(v, out int result))
            Set(result, UpdateOnChange);
        else
            Value = default(int).ToString();
    }
}

/// <summary>
/// Stores and retrieves bool values
/// </summary>
public class SaveBool : SaveVariable
{
    public static implicit operator bool(SaveBool obj) => obj.Get();

    public SaveBool(string savedName, SaveFile saveFile = null, bool defaultValue = false) : base(savedName, saveFile, defaultValue) { }

    public bool Get() => bool.TryParse(Value, out var b) && b;

    public void Set(bool v, bool UpdateOnChange = true)
    {
        Value = v.ToString();
        OnSet();

        if (UpdateOnChange)
        {
            SaveFile.WriteFile();
        }
    }

    public override object GetAsObject() => Get();
    public override void SetFromObject(object v, bool UpdateOnChange = true) => Set((bool)v, UpdateOnChange);

    public override string GetAsString() => Value;
    public override void SetFromString(string v, bool UpdateOnChange = true)
    {
        if (bool.TryParse(v, out bool result))
            Set(result, UpdateOnChange);
        else
            Value = default(bool).ToString();
    }
}

/// <summary>
/// Stores and retrieves string values
/// </summary>
public class SaveString : SaveVariable
{
    public static implicit operator string(SaveString obj) => obj.Get();

    public SaveString(string savedName, SaveFile saveFile = null, string defaultValue = default) : base(savedName, saveFile, defaultValue) { }

    public string Get() => Value ?? string.Empty;

    public void Set(string v, bool UpdateOnChange = true)
    {
        Value = v ?? "";
        OnSet();

        if (UpdateOnChange)
        {
            SaveFile.WriteFile();
        }
    }

    public override object GetAsObject() => Get();
    public override void SetFromObject(object v, bool UpdateOnChange = true) => Set((string)v, UpdateOnChange);

    //huh, guess these are kind of pointless here
    public override string GetAsString() => Value;
    public override void SetFromString(string v, bool UpdateOnChange = true) => Set(v, UpdateOnChange);
}

/// <summary>
/// Stores and retrieves Enum values
/// </summary>
public class SaveEnum<T> : SaveVariable where T : struct, Enum
{
    public static implicit operator T(SaveEnum<T> obj) => obj.Get();

    public SaveEnum(string savedName, SaveFile saveFile = null, T defaultValue = default) : base(savedName, saveFile, defaultValue) { }

    public T Get()
    {
        if (Enum.TryParse(Value, out T result))
            return result;
        return default; // fallback to first enum value
    }

    public void Set(T v, bool UpdateOnChange = true)
    {
        Value = v.ToString();
        OnSet();

        if (UpdateOnChange)
        {
            SaveFile.WriteFile();
        }
    }

    public override object GetAsObject() => Get();
    public override void SetFromObject(object v, bool UpdateOnChange = true) => Set((T)v, UpdateOnChange);

    public override string GetAsString() => Value;
    public override void SetFromString(string v, bool UpdateOnChange = true)
    {
        if (Enum.TryParse(v, out T result))
            Set(result, UpdateOnChange);
        else
            Value = default(T).ToString();
    }
}

/// <summary>
/// Stores and retrieves string values
/// Essentially the same as a SaveString. except it doesn't use the SavedString variable for compatability reasons
/// </summary>
public class SavePath : SaveVariable
{
    public static implicit operator string(SavePath obj) => obj.Get();

    public SavePath(string path, SaveFile saveFile = null) : base(path, saveFile) { }

    public string Get() => SavedName ?? string.Empty;

    public void Set(string v, bool UpdateOnChange = true)
    {
        SavedName = v ?? "";
        OnSet();

        if (UpdateOnChange)
        {
            SaveFile.WriteFile();
        }
    }

    public override object GetAsObject() => Get();
    public override void SetFromObject(object v, bool UpdateOnChange = true) => Set((string)v, UpdateOnChange);

    public override string GetAsString() => SavedName;
    public override void SetFromString(string v, bool UpdateOnChange = true) => Set(v, UpdateOnChange);

    internal override string SavedString() { return SavedName; }
}

/// <summary>
/// Stores and retrieves Color values
/// </summary>
public class SaveColor : SaveVariable
{
    public static implicit operator Color(SaveColor obj) => obj.Get();

    public SaveColor(string savedName, SaveFile saveFile = null, Color defaultValue = default) : base(savedName, saveFile, defaultValue) { }

    public Color Get() => ParseColor(Value);

    public void Set(Color v, bool UpdateOnChange = true)
    {
        Value = string.Join(",", v.r.ToString(CultureInfo.InvariantCulture), v.g.ToString(CultureInfo.InvariantCulture), v.b.ToString(CultureInfo.InvariantCulture), v.a.ToString(CultureInfo.InvariantCulture));
        OnSet();

        if (UpdateOnChange)
        {
            SaveFile.WriteFile();
        }
    }

    public void SetRed(float r, bool UpdateOnChange = true)
    {
        Set(new Color(r, Get().g, Get().b), UpdateOnChange);
    }

    public void SetGreen(float g, bool UpdateOnChange = true)
    {
        Set(new Color(Get().r, g, Get().b), UpdateOnChange);
    }

    public void SetBlue(float b, bool UpdateOnChange = true)
    {
        Set(new Color(Get().r, Get().g, b), UpdateOnChange);
    }

    public override object GetAsObject() => Get();

    public override void SetFromObject(object v, bool UpdateOnChange = true) => Set((Color)v, UpdateOnChange);

    public override string GetAsString() => Value;

    public override void SetFromString(string v, bool UpdateOnChange = true)
    {
        if (TryParseColor(v, out Color color))
            Set(color, UpdateOnChange);
        else
            Value = SerializeColor(Color.black);
    }

    private static Color ParseColor(string value)
    {
        return TryParseColor(value, out Color color) ? color : Color.black;
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

    private static string SerializeColor(Color color)
    {
        return string.Join(",",
            color.r.ToString(CultureInfo.InvariantCulture),
            color.g.ToString(CultureInfo.InvariantCulture),
            color.b.ToString(CultureInfo.InvariantCulture),
            color.a.ToString(CultureInfo.InvariantCulture));
    }
}