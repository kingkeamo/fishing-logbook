using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Profile.Clients;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Profile.Pages.PublicProfile;

public partial class PublicProfile : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private PublicProfileDto? _profile;
    private bool _isLoading = true;
    private bool _loadFailed;

    [Parameter]
    public Guid UserId { get; set; }

    [Inject]
    private IProfileClient ProfileClient { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    protected override async Task OnParametersSetAsync()
    {
        _isLoading = true;
        _loadFailed = false;
        try
        {
            _profile = await ProfileClient.GetPublicAsync(UserId, _cancellationTokenSource.Token);
        }
        catch (Exception)
        {
            _loadFailed = true;
            _profile = null;
        }
        finally
        {
            _isLoading = false;
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
