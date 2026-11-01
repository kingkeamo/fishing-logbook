using System.ComponentModel;
using FishingLogBook.Mobile.Features.Authentication.Services;
using FishingLogBook.Mobile.Features.Profile.Clients;
using FishingLogBook.Mobile.Localization;

namespace FishingLogBook.Mobile.Features.Profile.ViewModels;

public sealed class HeaderProfileViewModel : INotifyPropertyChanged
{
    private readonly IProfileClient _profiles;
    private readonly IAuthenticationService _authentication;
    private string _displayName = string.Empty;
    private string _initial = "?";

    public HeaderProfileViewModel(IProfileClient profiles, IAuthenticationService authentication, UiTextService text)
    {
        _profiles = profiles;
        _authentication = authentication;
        Text = text;
        Apply(CurrentEmail());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public UiTextService Text { get; }

    public string DisplayName => _displayName;

    public string Initial => _initial;

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        var email = CurrentEmail();
        if (_displayName.Length == 0)
        {
            Apply(email);
        }

        var result = await _profiles.GetAsync(cancellationToken);
        if (result.IsSuccess)
        {
            var name = result.Value?.DisplayName?.Trim();
            Apply(string.IsNullOrWhiteSpace(name) ? email : name);
            return;
        }

        if (_displayName.Length == 0)
        {
            Apply(email);
        }
    }

    private string CurrentEmail()
    {
        return _authentication.CurrentSession?.Email.Trim() ?? string.Empty;
    }

    private void Apply(string name)
    {
        Set(ref _displayName, name, nameof(DisplayName));
        Set(ref _initial, InitialFrom(name), nameof(Initial));
    }

    private static string InitialFrom(string name)
    {
        foreach (var character in name)
        {
            if (!char.IsWhiteSpace(character))
            {
                return char.ToUpperInvariant(character).ToString();
            }
        }

        return "?";
    }

    private void Set<T>(ref T field, T value, params string[] names)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        foreach (var name in names)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
