using System.Collections.Generic;
using System.IO;

public class FileNode
{
    public static readonly bool readAllFiles = false;

    public string Name { get; }
    public string Path { get; }
    public bool IsDirectory { get; }
    public FileNode Parent;

    public List<FileNode> Children { get; } = new();

    private static readonly HashSet<string> supportedFileTypes = new()
    {
        ".mp3",
        ".ogg",
        ".wav",
        ".m4a",
        ".m3u",
        ".m3u8",
    };

    private FileNode(string name, string path, bool isDirectory)
    {
        Name = name;
        Path = path;
        IsDirectory = isDirectory;
    }

    public static FileNode BuildTree(string DirectoryPath)
    {
        DirectoryInfo directory = new DirectoryInfo(DirectoryPath);

        FileNode node = new FileNode(
            directory.Name,
            directory.FullName,
            true
        );

        foreach (DirectoryInfo dir in directory.GetDirectories())
        {
            FileNode childNode = BuildTree(dir.FullName);
            node.Children.Add(childNode);
            childNode.Parent = node;
        }

        foreach (FileInfo file in directory.GetFiles())
        {
            if (!readAllFiles)
            {
                if (supportedFileTypes.Contains(file.Extension.ToLower()))
                    node.Children.Add(new FileNode(file.Name, file.FullName, false));
            }
            else
            {
                node.Children.Add(new FileNode(file.Name, file.FullName, false));
            }
        }

        return node;
    }

    public List<FileNode> GetAllChildren()
    {
        return GetAllChildren(this);
    }

    public static List<FileNode> GetAllChildren(FileNode node)
    {
        List<FileNode> result = new List<FileNode>();

        foreach (FileNode child in node.Children)
        {
            result.Add(child);
            result.AddRange(GetAllChildren(child));
        }

        return result;
    }
}