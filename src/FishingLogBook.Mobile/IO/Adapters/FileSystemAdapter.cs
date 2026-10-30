using System.Diagnostics.CodeAnalysis;

namespace FishingLogBook.Mobile.IO.Adapters;

[ExcludeFromCodeCoverage]
public sealed class FileSystemAdapter : IFileSystemAdapter
{
    public bool FileExists(string path)
    {
        return File.Exists(path);
    }

    public Stream OpenRead(string path)
    {
        return File.OpenRead(path);
    }

    public Stream Create(string path)
    {
        return File.Create(path);
    }

    public void CreateDirectory(string path)
    {
        Directory.CreateDirectory(path);
    }

    public void MoveFile(string sourcePath, string destinationPath, bool overwrite)
    {
        File.Move(sourcePath, destinationPath, overwrite);
    }

    public void DeleteFile(string path)
    {
        File.Delete(path);
    }

    public bool DirectoryExists(string path)
    {
        return Directory.Exists(path);
    }

    public IEnumerable<string> EnumerateFiles(string directory, string searchPattern)
    {
        return Directory.EnumerateFiles(directory, searchPattern);
    }
}
