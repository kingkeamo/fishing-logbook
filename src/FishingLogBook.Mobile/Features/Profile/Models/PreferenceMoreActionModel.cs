namespace FishingLogBook.Mobile.Features.Profile.Models;

public sealed class PreferenceMoreActionModel(MethodPreferenceEditModel? method)
{
    public MethodPreferenceEditModel? Method { get; } = method;
}
