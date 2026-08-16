using Microsoft.AspNetCore.Components;

namespace FishingLogBook.Web.Features.Authentication.Pages.Authentication;

public partial class Authentication : ComponentBase
{
    [Parameter]
    public string? Action { get; set; }
}
