using FishingLogBook.Shared.Enums;
using FishingLogBook.Shared.Loaders;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Components.FishingLoader;

public partial class FishingLoader : ComponentBase
{
    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    [Parameter]
    public LoaderKindEnum Kind { get; set; } = LoaderKindEnum.Random;

    [Parameter]
    public bool Compact { get; set; }

    [Parameter]
    public string? Label { get; set; }

    [Parameter]
    public string? Class { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private LoaderKindEnum _requestedKind = LoaderKindEnum.Random;
    private LoaderKindEnum _resolvedKind = LoaderKindEnum.LeapingFish;
    private bool _hasResolvedKind;

    private string AccessibleLabel => string.IsNullOrWhiteSpace(Label) ? Loc["Common_Loading"].Value : Label;

    protected override void OnParametersSet()
    {
        if (_hasResolvedKind && _requestedKind == Kind)
        {
            return;
        }

        _requestedKind = Kind;
        _resolvedKind = LoaderKindResolver.Resolve(Kind);
        _hasResolvedKind = true;
    }

    private string CssClass
    {
        get
        {
            var kind = _resolvedKind switch
            {
                LoaderKindEnum.Boat => "fishing-loader-boat",
                LoaderKindEnum.Bobber => "fishing-loader-bobber",
                _ => "fishing-loader-leap"
            };

            return string.Join(' ', new[] { "fishing-loader", kind, Compact ? "is-compact" : null, Class }.Where(part => !string.IsNullOrWhiteSpace(part)));
        }
    }
}
