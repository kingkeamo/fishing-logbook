using FishingLogBook.Mobile.Features.Import.Models;
using FishingLogBook.Mobile.IO.Adapters;

namespace FishingLogBook.Mobile.Features.Import.Services;

public sealed class ImportPhotoBlobRegistryService : IImportPhotoBlobRegistryService
{
    private readonly IFileSystemAdapter _files;
    private readonly string _root;
    private readonly Dictionary<string, string> _paths = [];
    private readonly object _gate = new();

    public ImportPhotoBlobRegistryService(IFileSystemAdapter files, string root)
    {
        _files = files;
        _root = root;
    }

    public Task<ImportPhotoBlobRegistrationModel> RegisterAsync(
        byte[] bytes,
        string contentType,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var token = Guid.NewGuid().ToString("N");
        _files.CreateDirectory(_root);
        var path = Path.Combine(_root, token);
        using (var stream = _files.Create(path))
        {
            stream.Write(bytes, 0, bytes.Length);
        }

        lock (_gate)
        {
            _paths[token] = path;
        }

        return Task.FromResult(new ImportPhotoBlobRegistrationModel(token, path));
    }

    public Task<byte[]> GetBytesAsync(string token, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string path;
        lock (_gate)
        {
            if (!_paths.TryGetValue(token, out path!))
            {
                throw new InvalidOperationException("The prepared photograph could not be read.");
            }
        }

        using var stream = _files.OpenRead(path);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return Task.FromResult(buffer.ToArray());
    }

    public Task RemoveAsync(string token, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string? path;
        lock (_gate)
        {
            _paths.Remove(token, out path);
        }

        if (path is not null && _files.FileExists(path))
        {
            _files.DeleteFile(path);
        }

        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string[] paths;
        lock (_gate)
        {
            paths = [.. _paths.Values];
            _paths.Clear();
        }

        foreach (var path in paths.Where(_files.FileExists))
        {
            _files.DeleteFile(path);
        }

        return Task.CompletedTask;
    }
}
