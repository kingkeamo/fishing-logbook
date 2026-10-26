using System.Globalization;
using FishingLogBook.Mobile.Interactions;
using FishingLogBook.Mobile.Localization;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Interactions.InteractionServiceTests;

public abstract class BaseInteractionServiceTest
{
    protected readonly IInteractionPresenter MockPresenter = Substitute.For<IInteractionPresenter>();
    protected readonly UiTextService Text = new(CultureInfo.GetCultureInfo("en-GB"));
    protected readonly DialogRequest Request = new("Reset demo?", "No data is changed.", "Reset", "Cancel", true);
    protected readonly InteractionService Sut;

    protected BaseInteractionServiceTest()
    {
        Sut = new InteractionService(MockPresenter, Text);
    }
}
