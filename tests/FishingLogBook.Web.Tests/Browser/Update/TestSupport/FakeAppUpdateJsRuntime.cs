using System.Text.Json;
using FishingLogBook.Web.Browser.Update;
using Microsoft.JSInterop;

namespace FishingLogBook.Web.Tests.Browser.Update.TestSupport;

public sealed class FakeAppUpdateJsRuntime : IJSRuntime, IJSObjectReference
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public const string NoUpdateJson = """{"isUpdateReady":false}""";
    public const string UpdateReadyJson = """{"isUpdateReady":true}""";

    public string StateJson { get; set; } = NoUpdateJson;

    public bool ApplyAccepted { get; set; } = true;

    public long SubscriptionToken { get; set; } = 9;

    public Exception? StateFailure { get; set; }

    public Exception? ApplyFailure { get; set; }

    public List<string> ImportedModules { get; } = [];

    public List<string> Invocations { get; } = [];

    public List<long> UnsubscribedTokens { get; } = [];

    public DotNetObjectReference<AppUpdateService>? Subscriber { get; private set; }

    public void Publish(string stateJson)
    {
        StateJson = stateJson;
        if (Subscriber is null)
        {
            throw new InvalidOperationException("No subscriber was registered.");
        }

        Subscriber.Value.OnUpdateStateChanged(Deserialise<AppUpdateState>(stateJson));
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
            case "getUpdateState":
                return StateFailure is null ? Deserialise<TValue>(StateJson) : throw StateFailure;
            case "subscribeUpdateState":
                Subscriber = (DotNetObjectReference<AppUpdateService>)args![0]!;
                return (TValue)(object)SubscriptionToken;
            case "applyUpdate":
                return ApplyFailure is null
                    ? (TValue)(object)ApplyAccepted
                    : throw ApplyFailure;
            case "unsubscribeUpdateState":
                UnsubscribedTokens.Add(Convert.ToInt64(args![0]!));
                return default!;
            default:
                throw new InvalidOperationException(identifier);
        }
    }

    private static TValue Deserialise<TValue>(string json)
    {
        return JsonSerializer.Deserialize<TValue>(json, Options)!;
    }
}
