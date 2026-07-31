using System.IO;
using System;

public static class GameVariables
{
    public const string gameNameChangeLater = "GameNameHereReplaceLater"; //DO NOT FORGET TO CHANGE THIS
    public readonly static string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), gameNameChangeLater);
}
