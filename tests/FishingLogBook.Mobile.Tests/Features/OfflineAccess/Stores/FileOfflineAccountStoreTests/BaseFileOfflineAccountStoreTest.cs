using System.Text;
using System.Text.Json;
using FishingLogBook.Mobile.Features.OfflineAccess.Models;
using FishingLogBook.Mobile.Features.OfflineAccess.Stores;
using FishingLogBook.Mobile.IO.Adapters;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.OfflineAccess.Stores.FileOfflineAccountStoreTests;

public abstract class BaseFileOfflineAccountStoreTest
{
    protected static readonly Guid UserId = Guid.Parse("7c0ca307-b9cd-4e8f-b020-eab7adfffe2a");
    protected static readonly string Directory = Path.Combine("app-data", "offline-accounts");
    protected readonly IFileSystemAdapter MockFileSystem = Substitute.For<IFileSystemAdapter>();
    protected readonly List<(string Path, MemoryStream Stream)> CreatedFiles = [];
    protected readonly FileOfflineAccountStore Sut;

    protected BaseFileOfflineAccountStoreTest()
    {
        MockFileSystem.Create(Arg.Any<string>()).Returns(call =>
        {
            var stream = new MemoryStream();
            CreatedFiles.Add((call.Arg<string>(), stream));
            return stream;
        });
        Sut = new FileOfflineAccountStore(MockFileSystem, Directory);
    }

    protected static OfflineAccountModel Account()
    {
        return new OfflineAccountModel(
            "cognito-subject",
            UserId,
            "angler@example.com",
            new ProfileDto(
                UserId,
                "Eamonn",
                null,
                null,
                null,
                "Connemara",
                true,
                false,
                true,
                true,
                true,
                WeightUnitEnum.Kg,
                LengthUnitEnum.Cm,
                true));
    }

    protected static string PathFor(Guid userId)
    {
        return Path.Combine(Directory, userId.ToString("N") + ".json");
    }

    protected void GivenStored(OfflineAccountModel account)
    {
        var json = JsonSerializer.Serialize(account, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        MockFileSystem.FileExists(PathFor(account.UserId)).Returns(true);
        MockFileSystem.OpenRead(PathFor(account.UserId))
            .Returns(_ => new MemoryStream(Encoding.UTF8.GetBytes(json)));
    }
}
