using Bunit;
using FishingLogBook.Web.Features.Import.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FishingLogBook.Web.Tests.Features.Import.Components.ImportPhotographPickerTests;

public class BaseImportPhotographPickerTest
{
    protected static BunitContext CreateContext(IImportPhotoPreparationService preparation)
    {
        var context = new BunitContext();
        context.Services.AddSingleton(preparation);
        return context;
    }
}
