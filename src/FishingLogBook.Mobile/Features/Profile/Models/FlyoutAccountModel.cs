namespace FishingLogBook.Mobile.Features.Profile.Models;

public sealed record FlyoutAccountModel(string PrimaryName, string SecondaryEmail, string Initial)
{
    public bool HasSecondaryEmail => SecondaryEmail.Length > 0;

    public static FlyoutAccountModel From(string? displayName, string? email)
    {
        var name = displayName?.Trim() ?? string.Empty;
        var mail = email?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            return new FlyoutAccountModel(mail, string.Empty, InitialFrom(mail));
        }

        if (mail.Length > 0 && !string.Equals(name, mail, StringComparison.OrdinalIgnoreCase))
        {
            return new FlyoutAccountModel(name, mail, InitialFrom(name));
        }

        return new FlyoutAccountModel(name, string.Empty, InitialFrom(name));
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
}
