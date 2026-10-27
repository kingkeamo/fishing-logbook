using System.Text.Json;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.IO.Adapters;

namespace FishingLogBook.Mobile.Features.Profile.Stores;

public sealed class FileAnglerPreferencesStore : IAnglerPreferencesStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _fileGate = new(1, 1);
    private readonly IFileSystemAdapter _fileSystem;
    private readonly string _directory;

    public FileAnglerPreferencesStore(IFileSystemAdapter fileSystem, string directory)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _fileSystem = fileSystem;
        _directory = directory;
    }

    public async Task<AnglerPreferencesModel?> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var path = GetPath(userId);
        await _fileGate.WaitAsync(cancellationToken);
        try
        {
            if (!_fileSystem.FileExists(path))
            {
                return null;
            }

            await using var stream = _fileSystem.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<AnglerPreferencesModel>(stream, SerializerOptions, cancellationToken);
        }
        finally
        {
            _fileGate.Release();
        }
    }

    public async Task SaveAsync(Guid userId, AnglerPreferencesModel preferences, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        var path = GetPath(userId);
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        var replaced = false;
        await _fileGate.WaitAsync(cancellationToken);
        try
        {
            _fileSystem.CreateDirectory(_directory);
            await using (var stream = _fileSystem.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(stream, preferences, SerializerOptions, cancellationToken);
            }

            _fileSystem.MoveFile(temporaryPath, path, overwrite: true);
            replaced = true;
        }
        finally
        {
            if (!replaced)
            {
                DeleteTemporaryFile(temporaryPath);
            }

            _fileGate.Release();
        }
    }

    private void DeleteTemporaryFile(string temporaryPath)
    {
        try
        {
            _fileSystem.DeleteFile(temporaryPath);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private string GetPath(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A signed-in account is required.", nameof(userId));
        }

        return Path.Combine(_directory, userId.ToString("N") + ".json");
    }
}
