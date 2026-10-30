using FishingLogBook.Mobile.IO.Adapters;

namespace FishingLogBook.Mobile.Tests.Support;

public sealed class TestFileSystemAdapter : IFileSystemAdapter
{
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);

    public bool FailNextWrite { get; set; }

    public void WriteFile(string path, byte[] content)
    {
        _files[path] = content;
    }

    public bool FileExists(string path)
    {
        return _files.ContainsKey(path);
    }

    public Stream OpenRead(string path)
    {
        if (!_files.TryGetValue(path, out var content))
        {
            throw new FileNotFoundException();
        }

        return new MemoryStream(content, writable: false);
    }

    public Stream Create(string path)
    {
        if (FailNextWrite)
        {
            FailNextWrite = false;
            throw new IOException("Simulated write failure.");
        }

        return new MemoryStream();
    }

    public void CreateDirectory(string path)
    {
    }

    public void MoveFile(string sourcePath, string destinationPath, bool overwrite)
    {
        if (!_files.ContainsKey(sourcePath))
        {
            if (File.Exists(sourcePath))
            {
                var bytes = File.ReadAllBytes(sourcePath);
                _files[destinationPath] = bytes;
                File.Delete(sourcePath);
                return;
            }

            throw new FileNotFoundException();
        }

        if (_files.ContainsKey(destinationPath) && !overwrite)
        {
            throw new IOException();
        }

        _files[destinationPath] = _files[sourcePath];
        _files.Remove(sourcePath);
    }

    public void DeleteFile(string path)
    {
        _files.Remove(path);
    }

    public bool DirectoryExists(string path)
    {
        return true;
    }

    public IEnumerable<string> EnumerateFiles(string directory, string searchPattern)
    {
        var prefix = directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return _files.Keys.Where(path => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }
}
