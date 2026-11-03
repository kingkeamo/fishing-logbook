using System.ComponentModel;
using FishingLogBook.Mobile.Features.Authentication.Services;
using FishingLogBook.Mobile.Features.Profile.Clients;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Localization;

namespace FishingLogBook.Mobile.Features.Profile.ViewModels;

public sealed class HeaderProfileViewModel : INotifyPropertyChanged
{
    private readonly IProfileClient _profiles;
    private readonly IAuthenticationService _authentication;
    private string _profileDisplayName = string.Empty;
    private FlyoutAccountModel _account = FlyoutAccountModel.From(null, null);

    public HeaderProfileViewModel(IProfileClient profiles, IAuthenticationService authentication, UiTextService text)
    {
        _profiles = profiles;
        _authentication = authentication;
        Text = text;
        Apply();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public UiTextService Text { get; }

    public string DisplayName => _account.PrimaryName;

    public string SecondaryEmail => _account.SecondaryEmail;

    public bool HasSecondaryEmail => _account.HasSecondaryEmail;

    public string Initial => _account.Initial;

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        Apply();
        var result = await _profiles.GetAsync(cancellationToken);
        if (result.IsSuccess)
        {
            _profileDisplayName = result.Value?.DisplayName?.Trim() ?? string.Empty;
        }

        Apply();
    }

    public void RefreshSession()
    {
        Apply();
    }

    private void Apply()
    {
        var account = FlyoutAccountModel.From(_profileDisplayName, CurrentEmail());
        if (_account == account)
        {
            return;
        }

        _account = account;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    private string CurrentEmail()
    {
        return _authentication.CurrentSession?.Email.Trim() ?? string.Empty;
    }
}
