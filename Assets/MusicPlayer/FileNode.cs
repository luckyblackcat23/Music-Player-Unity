using System.Collections.Generic;
using System.IO;

public class FileNode
{
    public string Name { get; }
    public string Path { get; }
    public bool IsDirectory { get; }

    public List<FileNode> Children { get; } = new();

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
            node.Children.Add(BuildTree(dir.FullName));

        foreach (var file in directory.GetFiles())
            node.Children.Add(
                new FileNode(file.Name, file.FullName, false)
            );

        return node;
    }

    public List<FileNode> GetAllChildren()
    {
        return GetAllChildren(this);
    }

    public static List<FileNode> GetAllChildren(FileNode node)
    {
        var result = new List<FileNode>();

        foreach (var child in node.Children)
        {
            result.Add(child);
            result.AddRange(GetAllChildren(child));
        }

        return result;
    }
}