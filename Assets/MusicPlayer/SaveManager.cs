using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

//technically, it's more efficient to store the data as raw binary instead of string data. this method was simpler for me to program though
public static class SaveManager
{
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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
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

    public SaveFile(string fileName, string fileEnding = ".txt", string savePath = null)
    {
        SavedName = fileName ?? throw new ArgumentNullException(nameof(fileName));

        SavedPath = savePath ?? Path.Combine(Globals.SaveFolderPath, fileName);

        if (!File.Exists(SavedPath))
        {
            File.CreateText(SavedPath);

            WriteFile();

            Debug.Log($"File '{fileName}' does not exist. creating File");
        }

        SaveManager.RegisterFile(this);

        using FileSystemWatcher watcher = new FileSystemWatcher(Globals.SaveFolderPath);
        
        watcher.Changed += (object sender, FileSystemEventArgs e) => WriteVariables();

        watcher.Filter = $"{fileName}";
        watcher.EnableRaisingEvents = true;

        Application.quitting += () => watcher.Dispose();
    }

    /// <summary>
    /// Updates the file to match the SaveVariables
    /// </summary>
    internal virtual void WriteFile(bool updateCacheAfter = true)
    {
        try
        {
            using StreamWriter sw = new StreamWriter(SavedPath, false);
            foreach (var variable in Variables)
            {
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

        foreach (SaveVariable variable in Variables)
        {
            foreach (string line in cachedText)
            {
                if (line.StartsWith($"{variable.SavedName}="))
                {
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

    // enforce that all derived classes must implement Set and Get
    public abstract object GetAsObject();
    public abstract void SetFromObject(object v, bool UpdateOnChange = true);

    public abstract string GetAsString();
    public abstract void SetFromString(string v, bool UpdateOnChange = true);

    internal virtual string SavedString() => $"{SavedName}={Value}";

    protected SaveVariable(string savedName, SaveFile saveFile = null)
    {
        SavedName = savedName ?? throw new ArgumentNullException(nameof(savedName));

        // decide which file this variable belongs to
        SaveFile = saveFile ?? SaveManager.MainSave();

        if (!saveFile.Variables.Contains(this))
            SaveFile.Variables.Add(this);
        else
        {
            Debug.Log("unable to save this variable as it already exists in the save manager");
        }
    }
}


/// <summary>
/// Stores and retrieves float values
/// </summary>
public class SaveFloat : SaveVariable
{
    public static implicit operator float(SaveFloat obj) => obj.Get();

    public SaveFloat(string savedName, SaveFile saveFile = null) : base(savedName, saveFile) { }

    public float Get() => float.TryParse(Value, out var f) ? f : 0f;

    public void Set(float v, bool UpdateOnChange = true)
    {
        Value = v.ToString();

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

    public SaveInt(string savedName, SaveFile saveFile = null) : base(savedName, saveFile) { }

    public int Get() => int.TryParse(Value, out var i) ? i : 0;

    public void Set(int v, bool UpdateOnChange = true)
    {
        Value = v.ToString();

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

    public SaveBool(string savedName, SaveFile saveFile = null) : base(savedName, saveFile) { }

    public bool Get() => bool.TryParse(Value, out var b) && b;

    public void Set(bool v, bool UpdateOnChange = true)
    {
        Value = v.ToString();

        if (UpdateOnChange)
        {
            SaveFile.WriteFile();
        }
    }

    public override object GetAsObject() => Get();
    public override void SetFromObject(object v, bool UpdateOnChange = true) => Set((bool)v);

    public override string GetAsString() => Value;
    public override void SetFromString(string v, bool UpdateOnChange = true)
    {
        if (bool.TryParse(v, out bool result))
            Set(result);
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

    public SaveString(string savedName, SaveFile saveFile = null) : base(savedName, saveFile) { }

    public string Get() => Value ?? string.Empty;

    public void Set(string v, bool UpdateOnChange = true)
    {
        Value = v ?? "";

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

    public SaveEnum(string savedName, SaveFile saveFile = null) : base(savedName, saveFile) { }

    public T Get()
    {
        if (Enum.TryParse(Value, out T result))
            return result;
        return default; // fallback to first enum value
    }

    public void Set(T v, bool UpdateOnChange = true)
    {
        Value = v.ToString();

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