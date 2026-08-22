using System.Text.Json;
using FishingLogBook.Web.Browser.Install;
using Microsoft.JSInterop;

namespace FishingLogBook.Web.Tests.Browser.Install.TestSupport;

public sealed class FakeInstallJsRuntime : IJSRuntime, IJSObjectReference
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public const string UnknownStateJson =
        """{"isInstalled":false,"canPrompt":false,"platformFamily":"Other","isSafari":false}""";

    public string StateJson { get; set; } = UnknownStateJson;

    public string PromptOutcome { get; set; } = "unavailable";

    public long SubscriptionToken { get; set; } = 42;

    public Exception? StateFailure { get; set; }

    public Exception? SubscribeFailure { get; set; }

    public List<string> ImportedModules { get; } = [];

    public List<string> Invocations { get; } = [];

    public List<long> UnsubscribedTokens { get; } = [];

    public DotNetObjectReference<InstallStateSubscription>? Subscriber { get; private set; }

    public Task PublishAsync(string stateJson)
    {
        StateJson = stateJson;
        if (Subscriber is null)
        {
            throw new InvalidOperationException("No subscriber was registered.");
        }

        return Subscriber.Value.OnInstallStateChanged(Deserialise<InstallState>(stateJson));
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
    {
        return InvokeAsync<TValue>(identifier, CancellationToken.None, args);
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        Invocations.Add(identifier);
        return new ValueTask<TValue>(Invoke<TValue>(identifier, args));
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    private TValue Invoke<TValue>(string identifier, object?[]? args)
    {
        switch (identifier)
        {
            case "import":
                ImportedModules.Add((string)args![0]!);
                return (TValue)(object)this;
            case "getInstallState":
                return StateFailure is null ? Deserialise<TValue>(StateJson) : throw StateFailure;
            case "promptInstall":
                return (TValue)(object)PromptOutcome;
            case "subscribeInstallState":
                return Subscribe<TValue>(args);
            case "unsubscribeInstallState":
                UnsubscribedTokens.Add(Convert.ToInt64(args![0]!));
                return default!;
            default:
                throw new InvalidOperationException(identifier);
        }
    }

    private TValue Subscribe<TValue>(object?[]? args)
    {
        if (SubscribeFailure is not null)
        {
            throw SubscribeFailure;
        }

        Subscriber = (DotNetObjectReference<InstallStateSubscription>)args![0]!;
        return (TValue)(object)SubscriptionToken;
    }

    private static TValue Deserialise<TValue>(string json)
    {
        return JsonSerializer.Deserialize<TValue>(json, Options)!;
    }
}
