using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Profile.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Profile.Pages.Profile;

public partial class Profile : ComponentBase, IDisposable
{
    private const long MaxPhotographBytes = 10 * 1024 * 1024;

    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private string? _displayName;
    private string? _homeRegion;
    private string _preferredSpeciesText = string.Empty;
    private IReadOnlyCollection<string> _selectedFishingTypes = [];
    private bool _showDisplayName = true;
    private bool _showPhotograph;
    private bool _showHomeRegion;
    private bool _showPreferredFishingTypes;
    private bool _showPreferredSpecies;
    private bool _isLoading = true;
    private bool _isSaving;
    private bool _loadFailed;
    private bool _saveFailed;
    private string? _photographUrl;
    private byte[]? _pendingPhotographBytes;
    private string? _pendingPhotographContentType;

    [Inject]
    private IProfileClient ProfileClient { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _isLoading = true;
        _loadFailed = false;
        try
        {
            Apply(await ProfileClient.GetOwnAsync(_cancellationTokenSource.Token));
        }
        catch (Exception)
        {
            _loadFailed = true;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task SaveAsync()
    {
        if (_isSaving)
        {
            return;
        }

        _isSaving = true;
        _saveFailed = false;
        try
        {
            var saved = await ProfileClient.UpdateOwnAsync(BuildUpdate(), _cancellationTokenSource.Token);
            saved = await SavePhotographAsync(saved);
            Apply(saved);
        }
        catch (Exception)
        {
            _saveFailed = true;
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task<ProfileDto> SavePhotographAsync(ProfileDto saved)
    {
        if (_pendingPhotographBytes is null || string.IsNullOrWhiteSpace(_pendingPhotographContentType))
        {
            return saved;
        }

        var photographId = Guid.NewGuid();
        var upload = await ProfileClient.CreatePhotographUploadAsync(
            new PhotographUploadRequestDto(photographId, _pendingPhotographContentType),
            _cancellationTokenSource.Token);
        await ProfileClient.UploadPhotographAsync(
            upload.UploadUrl,
            _pendingPhotographBytes,
            _pendingPhotographContentType,
            _cancellationTokenSource.Token);
        var recorded = await ProfileClient.RecordPhotographAsync(
            new RecordPhotographDto(photographId, upload.ObjectKey, _pendingPhotographContentType),
            _cancellationTokenSource.Token);
        _pendingPhotographBytes = null;
        _pendingPhotographContentType = null;
        return recorded;
    }

    private async Task OnPhotographSelected(InputFileChangeEventArgs args)
    {
        var file = args.File;
        var contentType = string.IsNullOrWhiteSpace(file.ContentType)
            ? PhotographContentTypeConstants.Jpeg
            : file.ContentType;
        if (!PhotographContentTypeConstants.IsAllowed(contentType))
        {
            return;
        }

        await using var stream = file.OpenReadStream(MaxPhotographBytes);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, _cancellationTokenSource.Token);
        _pendingPhotographBytes = buffer.ToArray();
        _pendingPhotographContentType = contentType;
        _photographUrl = $"data:{_pendingPhotographContentType};base64,{Convert.ToBase64String(_pendingPhotographBytes)}";
    }

    private void Apply(ProfileDto profile)
    {
        _displayName = profile.DisplayName;
        _homeRegion = profile.HomeRegion;
        _selectedFishingTypes = [.. profile.PreferredFishingTypes];
        _preferredSpeciesText = string.Join(", ", profile.PreferredSpecies);
        _showDisplayName = profile.ShowDisplayName;
        _showPhotograph = profile.ShowPhotograph;
        _showHomeRegion = profile.ShowHomeRegion;
        _showPreferredFishingTypes = profile.ShowPreferredFishingTypes;
        _showPreferredSpecies = profile.ShowPreferredSpecies;
        if (_pendingPhotographBytes is null)
        {
            _photographUrl = profile.PhotographUrl;
        }
    }

    private UpdateProfileDto BuildUpdate()
    {
        var species = _preferredSpeciesText
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return new UpdateProfileDto(
            _displayName,
            _homeRegion,
            _selectedFishingTypes.ToArray(),
            species,
            _showDisplayName,
            _showPhotograph,
            _showHomeRegion,
            _showPreferredFishingTypes,
            _showPreferredSpecies);
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
