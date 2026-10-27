namespace FishingLogBook.Mobile.IO.Adapters;

public interface IFileSystemAdapter
{
    bool FileExists(string path);

    Stream OpenRead(string path);

    Stream Create(string path);

    void CreateDirectory(string path);

    void MoveFile(string sourcePath, string destinationPath, bool overwrite);

    void DeleteFile(string path);
}
