using Bunit;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Onboarding.Services;
using FishingLogBook.Web.Features.Profile.Clients;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Onboarding.Services.OnboardingServiceTests;

public class BaseOnboardingServiceTest : IDisposable
{
    protected const string GetItem = "localStorage.getItem";
    protected const string SetItem = "localStorage.setItem";

    protected static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    protected static readonly string CacheKey = $"fishingLogBook.onboardingCompleted.{UserId:D}";

    protected readonly ILocalCatchOwnerService MockOwnerService = Substitute.For<ILocalCatchOwnerService>();
    protected readonly IProfileClient MockProfileClient = Substitute.For<IProfileClient>();
    protected readonly BunitContext Context = new();
    protected readonly OnboardingService Sut;

    protected BaseOnboardingServiceTest()
    {
        MockOwnerService.GetUserIdAsync(Arg.Any<CancellationToken>()).Returns(UserId);
        Sut = new OnboardingService(
            MockOwnerService,
            MockProfileClient,
            Context.Services.GetRequiredService<IJSRuntime>());
    }

    public void Dispose()
    {
        Context.Dispose();
        GC.SuppressFinalize(this);
    }

    protected void CacheContains(string? value)
    {
        Context.JSInterop.Setup<string?>(GetItem, CacheKey).SetResult(value);
    }

    protected void CacheCannotBeRead()
    {
        Context.JSInterop.Setup<string?>(GetItem, CacheKey).SetException(new JSException("blocked"));
    }

    protected void CacheAcceptsWrites()
    {
        Context.JSInterop.SetupVoid(SetItem, _ => true).SetVoidResult();
    }

    protected void CacheRejectsWrites()
    {
        Context.JSInterop.SetupVoid(SetItem, _ => true).SetException(new JSException("quota"));
    }

    protected IReadOnlyList<JSRuntimeInvocation> Writes()
    {
        return Context.JSInterop.Invocations[SetItem].ToList();
    }

    protected static ProfileDto Profile(bool onboardingCompleted)
    {
        return new ProfileDto(
            UserId,
            "Eamonn",
            null,
            null,
            null,
            "Westmeath",
            true,
            false,
            true,
            true,
            false,
            OnboardingCompleted: onboardingCompleted);
    }
}
