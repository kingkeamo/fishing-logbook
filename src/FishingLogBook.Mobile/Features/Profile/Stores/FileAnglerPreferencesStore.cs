using System.Text.Json;
using FishingLogBook.Mobile.Features.Profile.Models;

namespace FishingLogBook.Mobile.Features.Profile.Stores;

public sealed class FileAnglerPreferencesStore : IAnglerPreferencesStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly string _directory;

    public FileAnglerPreferencesStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public async Task<AnglerPreferencesModel?> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var path = GetPath(userId);
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<AnglerPreferencesModel>(stream, SerializerOptions, cancellationToken);
    }

    public async Task SaveAsync(Guid userId, AnglerPreferencesModel preferences, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        var path = GetPath(userId);
        var temporaryPath = path + ".tmp";
        Directory.CreateDirectory(_directory);
        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, preferences, SerializerOptions, cancellationToken);
        }

        File.Move(temporaryPath, path, overwrite: true);
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
