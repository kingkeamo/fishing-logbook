using System.Text;
using System.Text.Json;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Features.Profile.Stores;
using FishingLogBook.Mobile.IO.Adapters;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Profile.Stores.FileAnglerPreferencesStoreTests;

public abstract class BaseFileAnglerPreferencesStoreTest
{
    protected static readonly Guid FirstUserId = Guid.Parse("5b1f0c6a-2d3e-4f50-8a71-9c2b3d4e5f60");
    protected static readonly Guid SecondUserId = Guid.Parse("e7d6c5b4-a392-4180-9f7e-6d5c4b3a2918");
    protected static readonly string Directory = Path.Combine("app-data", "angler-preferences");
    protected static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    protected readonly IFileSystemAdapter MockFileSystem = Substitute.For<IFileSystemAdapter>();
    protected readonly List<(string Path, MemoryStream Stream)> CreatedFiles = [];
    protected readonly FileAnglerPreferencesStore Sut;

    protected BaseFileAnglerPreferencesStoreTest()
    {
        MockFileSystem.Create(Arg.Any<string>()).Returns(call =>
        {
            var stream = new MemoryStream();
            lock (CreatedFiles)
            {
                CreatedFiles.Add((call.Arg<string>(), stream));
            }

            return stream;
        });
        Sut = new FileAnglerPreferencesStore(MockFileSystem, Directory);
    }

    protected static string PathFor(Guid userId)
    {
        return Path.Combine(Directory, userId.ToString("N") + ".json");
    }

    protected static bool IsTemporaryPathFor(string path, Guid userId)
    {
        return path.StartsWith(PathFor(userId) + ".", StringComparison.Ordinal) &&
            path.EndsWith(".tmp", StringComparison.Ordinal);
    }

    protected static AnglerPreferencesModel? Deserialize(MemoryStream stream)
    {
        return JsonSerializer.Deserialize<AnglerPreferencesModel>(stream.ToArray(), SerializerOptions);
    }

    protected void GivenStoredJson(Guid userId, string json)
    {
        MockFileSystem.FileExists(PathFor(userId)).Returns(true);
        MockFileSystem.OpenRead(PathFor(userId)).Returns(_ => new MemoryStream(Encoding.UTF8.GetBytes(json)));
    }
}
