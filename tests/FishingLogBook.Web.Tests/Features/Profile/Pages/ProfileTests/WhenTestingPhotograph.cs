using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Profile.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components.Forms;
using NSubstitute;
using ProfilePage = FishingLogBook.Web.Features.Profile.Pages.Profile.Profile;

namespace FishingLogBook.Web.Tests.Features.Profile.Pages.ProfileTests;

public class WhenTestingPhotograph : BaseProfileTest
{
    [Fact]
    public async Task ItShouldIgnoreAnUnsupportedImageType()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var userId = Guid.NewGuid();
        var profileClient = Substitute.For<IProfileClient>();
        profileClient.GetOwnAsync(Arg.Any<CancellationToken>())
            .Returns(EmptyProfile(userId));
        profileClient.UpdateOwnAsync(Arg.Any<UpdateProfileDto>(), Arg.Any<CancellationToken>())
            .Returns(call => ToSaved(userId, call.ArgAt<UpdateProfileDto>(0)));
        await using var context = CreateContext(profileClient);
        var cut = context.Render<ProfilePage>();
        cut.WaitForAssertion(() => cut.Find("#profile-photo-input"));

        // Act
        cut.FindComponents<InputFile>()[0].UploadFiles(
            InputFileContent.CreateFromBinary([1, 2, 3], "photo.gif", contentType: "image/gif"));
        await cut.Find("#profile-save-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.FindAll("#profile-photo-preview").Should().BeEmpty());
        await profileClient.Received(1).UpdateOwnAsync(
            Arg.Any<UpdateProfileDto>(),
            Arg.Any<CancellationToken>());
        await profileClient.DidNotReceive().CreatePhotographUploadAsync(
            Arg.Any<PhotographUploadRequestDto>(),
            Arg.Any<CancellationToken>());
        await profileClient.DidNotReceive().UploadPhotographAsync(
            Arg.Any<string>(),
            Arg.Any<byte[]>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
        await profileClient.DidNotReceive().RecordPhotographAsync(
            Arg.Any<RecordPhotographDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotStartThePhotographSequenceWhenProfileSaveFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var profileClient = Substitute.For<IProfileClient>();
        profileClient.GetOwnAsync(Arg.Any<CancellationToken>())
            .Returns(EmptyProfile());
        profileClient.UpdateOwnAsync(Arg.Any<UpdateProfileDto>(), Arg.Any<CancellationToken>())
            .Returns<ProfileDto>(_ => throw new HttpRequestException("Unable to save profile."));
        await using var context = CreateContext(profileClient);
        var cut = context.Render<ProfilePage>();
        cut.WaitForAssertion(() => cut.Find("#profile-photo-input"));
        cut.FindComponents<InputFile>()[0].UploadFiles(
            InputFileContent.CreateFromBinary([0xFF, 0xD8, 0xFF], "photo.jpg", contentType: PhotographContentTypeConstants.Jpeg));

        // Act
        await cut.Find("#profile-save-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#profile-save-failed"));
        await profileClient.DidNotReceive().CreatePhotographUploadAsync(
            Arg.Any<PhotographUploadRequestDto>(),
            Arg.Any<CancellationToken>());
        await profileClient.DidNotReceive().UploadPhotographAsync(
            Arg.Any<string>(),
            Arg.Any<byte[]>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
        await profileClient.DidNotReceive().RecordPhotographAsync(
            Arg.Any<RecordPhotographDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotUploadOrRecordWhenCreatingTheUploadUrlFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var userId = Guid.NewGuid();
        var profileClient = Substitute.For<IProfileClient>();
        profileClient.GetOwnAsync(Arg.Any<CancellationToken>())
            .Returns(EmptyProfile(userId));
        profileClient.UpdateOwnAsync(Arg.Any<UpdateProfileDto>(), Arg.Any<CancellationToken>())
            .Returns(call => ToSaved(userId, call.ArgAt<UpdateProfileDto>(0)));
        profileClient.CreatePhotographUploadAsync(Arg.Any<PhotographUploadRequestDto>(), Arg.Any<CancellationToken>())
            .Returns<PhotographUploadDto>(_ => throw new HttpRequestException("Unable to create upload."));
        await using var context = CreateContext(profileClient);
        var cut = context.Render<ProfilePage>();
        cut.WaitForAssertion(() => cut.Find("#profile-photo-input"));
        cut.FindComponents<InputFile>()[0].UploadFiles(
            InputFileContent.CreateFromBinary([0xFF, 0xD8, 0xFF], "photo.jpg", contentType: PhotographContentTypeConstants.Jpeg));

        // Act
        await cut.Find("#profile-save-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#profile-save-failed"));
        await profileClient.Received(1).CreatePhotographUploadAsync(
            Arg.Any<PhotographUploadRequestDto>(),
            Arg.Any<CancellationToken>());
        await profileClient.DidNotReceive().UploadPhotographAsync(
            Arg.Any<string>(),
            Arg.Any<byte[]>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
        await profileClient.DidNotReceive().RecordPhotographAsync(
            Arg.Any<RecordPhotographDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotRecordWhenBinaryUploadFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var userId = Guid.NewGuid();
        var photographId = Guid.NewGuid();
        var objectKey = $"profiles/{userId:D}/{photographId:D}";
        var profileClient = Substitute.For<IProfileClient>();
        profileClient.GetOwnAsync(Arg.Any<CancellationToken>())
            .Returns(EmptyProfile(userId));
        profileClient.UpdateOwnAsync(Arg.Any<UpdateProfileDto>(), Arg.Any<CancellationToken>())
            .Returns(call => ToSaved(userId, call.ArgAt<UpdateProfileDto>(0)));
        profileClient.CreatePhotographUploadAsync(Arg.Any<PhotographUploadRequestDto>(), Arg.Any<CancellationToken>())
            .Returns(new PhotographUploadDto(objectKey, "https://storage.test/upload"));
        profileClient.UploadPhotographAsync(
                Arg.Any<string>(),
                Arg.Any<byte[]>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => throw new HttpRequestException("Upload failed."));
        await using var context = CreateContext(profileClient);
        var cut = context.Render<ProfilePage>();
        cut.WaitForAssertion(() => cut.Find("#profile-photo-input"));
        cut.FindComponents<InputFile>()[0].UploadFiles(
            InputFileContent.CreateFromBinary([0xFF, 0xD8, 0xFF], "photo.jpg", contentType: PhotographContentTypeConstants.Jpeg));

        // Act
        await cut.Find("#profile-save-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#profile-save-failed"));
        await profileClient.Received(1).UploadPhotographAsync(
            "https://storage.test/upload",
            Arg.Is<byte[]>(bytes => bytes.SequenceEqual(new byte[] { 0xFF, 0xD8, 0xFF })),
            PhotographContentTypeConstants.Jpeg,
            Arg.Any<CancellationToken>());
        await profileClient.DidNotReceive().RecordPhotographAsync(
            Arg.Any<RecordPhotographDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowSaveFailureWhenRecordingThePhotographFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var userId = Guid.NewGuid();
        Guid? photographId = null;
        var profileClient = Substitute.For<IProfileClient>();
        profileClient.GetOwnAsync(Arg.Any<CancellationToken>())
            .Returns(EmptyProfile(userId));
        profileClient.UpdateOwnAsync(Arg.Any<UpdateProfileDto>(), Arg.Any<CancellationToken>())
            .Returns(call => ToSaved(userId, call.ArgAt<UpdateProfileDto>(0)));
        profileClient.CreatePhotographUploadAsync(Arg.Any<PhotographUploadRequestDto>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.ArgAt<PhotographUploadRequestDto>(0);
                photographId = request.PhotographId;
                return new PhotographUploadDto(
                    $"profiles/{userId:D}/{request.PhotographId:D}",
                    "https://storage.test/upload");
            });
        profileClient.RecordPhotographAsync(Arg.Any<RecordPhotographDto>(), Arg.Any<CancellationToken>())
            .Returns<ProfileDto>(_ => throw new HttpRequestException("Record failed."));
        await using var context = CreateContext(profileClient);
        var cut = context.Render<ProfilePage>();
        cut.WaitForAssertion(() => cut.Find("#profile-photo-input"));
        cut.FindComponents<InputFile>()[0].UploadFiles(
            InputFileContent.CreateFromBinary([0xFF, 0xD8, 0xFF], "photo.jpg", contentType: PhotographContentTypeConstants.Jpeg));

        // Act
        await cut.Find("#profile-save-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#profile-save-failed"));
        photographId.Should().NotBeNull();
        var expectedKey = $"profiles/{userId:D}/{photographId:D}";
        await profileClient.Received(1).CreatePhotographUploadAsync(
            Arg.Is<PhotographUploadRequestDto>(request =>
                request.PhotographId == photographId
                && request.ContentType == PhotographContentTypeConstants.Jpeg),
            Arg.Any<CancellationToken>());
        await profileClient.Received(1).UploadPhotographAsync(
            "https://storage.test/upload",
            Arg.Is<byte[]>(bytes => bytes.SequenceEqual(new byte[] { 0xFF, 0xD8, 0xFF })),
            PhotographContentTypeConstants.Jpeg,
            Arg.Any<CancellationToken>());
        await profileClient.Received(1).RecordPhotographAsync(
            Arg.Is<RecordPhotographDto>(request =>
                request.PhotographId == photographId
                && request.ObjectKey == expectedKey
                && request.ContentType == PhotographContentTypeConstants.Jpeg),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowAPreviewThenUploadTheSelectedImageThroughTheFullPhotographSequence()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var userId = Guid.NewGuid();
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF };
        Guid? photographId = null;
        var profileClient = Substitute.For<IProfileClient>();
        profileClient.GetOwnAsync(Arg.Any<CancellationToken>())
            .Returns(EmptyProfile(userId));
        profileClient.UpdateOwnAsync(Arg.Any<UpdateProfileDto>(), Arg.Any<CancellationToken>())
            .Returns(call => ToSaved(userId, call.ArgAt<UpdateProfileDto>(0)));
        profileClient.CreatePhotographUploadAsync(Arg.Any<PhotographUploadRequestDto>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.ArgAt<PhotographUploadRequestDto>(0);
                photographId = request.PhotographId;
                return new PhotographUploadDto(
                    $"profiles/{userId:D}/{request.PhotographId:D}",
                    "https://storage.test/upload");
            });
        profileClient.RecordPhotographAsync(Arg.Any<RecordPhotographDto>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.ArgAt<RecordPhotographDto>(0);
                return new ProfileDto(
                    userId,
                    null,
                    request.PhotographId,
                    "https://storage.test/download",
                    request.ContentType,
                    null,
                    [],
                    [],
                    true,
                    false,
                    false,
                    false,
                    false);
            });
        await using var context = CreateContext(profileClient);
        var cut = context.Render<ProfilePage>();
        cut.WaitForAssertion(() => cut.Find("#profile-photo-input"));

        // Act
        cut.FindComponents<InputFile>()[0].UploadFiles(
            InputFileContent.CreateFromBinary(bytes, "photo.jpg", contentType: PhotographContentTypeConstants.Jpeg));
        cut.WaitForAssertion(() => cut.Find("#profile-photo-preview").GetAttribute("src").Should().StartWith("data:image/jpeg;base64,"));
        await cut.Find("#profile-save-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.FindAll("#profile-save-failed").Should().BeEmpty());
        photographId.Should().NotBeNull();
        var expectedKey = $"profiles/{userId:D}/{photographId:D}";
        await profileClient.Received(1).UpdateOwnAsync(
            Arg.Any<UpdateProfileDto>(),
            Arg.Any<CancellationToken>());
        await profileClient.Received(1).CreatePhotographUploadAsync(
            Arg.Is<PhotographUploadRequestDto>(request =>
                request.PhotographId == photographId
                && request.ContentType == PhotographContentTypeConstants.Jpeg),
            Arg.Any<CancellationToken>());
        await profileClient.Received(1).UploadPhotographAsync(
            "https://storage.test/upload",
            Arg.Is<byte[]>(actual => actual.SequenceEqual(bytes)),
            PhotographContentTypeConstants.Jpeg,
            Arg.Any<CancellationToken>());
        await profileClient.Received(1).RecordPhotographAsync(
            Arg.Is<RecordPhotographDto>(request =>
                request.PhotographId == photographId
                && request.ObjectKey == expectedKey
                && request.ContentType == PhotographContentTypeConstants.Jpeg),
            Arg.Any<CancellationToken>());
    }
}
