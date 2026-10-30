using System.Runtime.Versioning;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using FishingLogBook.Mobile.Features.Authentication.Handlers;
using FishingLogBook.Mobile.Features.OfflineAccess.Enums;
using FishingLogBook.Mobile.Native.Browsers;
using Microsoft.Extensions.DependencyInjection;
using AndroidLog = Android.Util.Log;

namespace FishingLogBook.Mobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop,
    Exported = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
        ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
[IntentFilter(
    [Intent.ActionView],
    Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable],
    DataScheme = "https",
    DataHost = "fishing-logbook-dev.pages.dev",
    DataPaths = ["/native/oauth2redirect", "/native/signedout"],
    AutoVerify = true)]
public class MainActivity : MauiAppCompatActivity
{
    private const int DeviceUnlockRequestCode = 26801;
    private readonly object _deviceUnlockLock = new();
    private TaskCompletionSource<DeviceUnlockResultEnum>? _deviceUnlockCompletion;
    private CancellationSignal? _deviceUnlockCancellation;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        MauiAuthenticationBrowser.RegisterAuthTab(this);
        DispatchAuthenticationCallback(Intent);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        Intent = intent;
        DispatchAuthenticationCallback(intent);
    }

    public Task<DeviceUnlockResultEnum> VerifyDeviceOwnerAsync(
        string reason,
        CancellationToken cancellationToken)
    {
        var keyguard = (KeyguardManager?)GetSystemService(KeyguardService);
        if (keyguard?.IsDeviceSecure != true)
        {
            return Task.FromResult(DeviceUnlockResultEnum.Unavailable);
        }

        TaskCompletionSource<DeviceUnlockResultEnum> completion;
        lock (_deviceUnlockLock)
        {
            if (_deviceUnlockCompletion is not null)
            {
                return Task.FromResult(DeviceUnlockResultEnum.Failed);
            }

            completion = new TaskCompletionSource<DeviceUnlockResultEnum>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _deviceUnlockCompletion = completion;
        }

        var registration = cancellationToken.Register(() =>
            CompleteDeviceUnlock(DeviceUnlockResultEnum.Cancelled));
        _ = completion.Task.ContinueWith(
            _ => registration.Dispose(),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        RunOnUiThread(() =>
        {
            if (completion.Task.IsCompleted)
            {
                return;
            }

            try
            {
                if (OperatingSystem.IsAndroidVersionAtLeast(29))
                {
                    StartDeviceAuthentication(reason);
                    return;
                }

                var intent = keyguard.CreateConfirmDeviceCredentialIntent(reason, reason);
                if (intent is null)
                {
                    CompleteDeviceUnlock(DeviceUnlockResultEnum.Unavailable);
                    return;
                }

                StartActivityForResult(intent, DeviceUnlockRequestCode);
            }
            catch (Exception)
            {
                CompleteDeviceUnlock(DeviceUnlockResultEnum.Failed);
            }
        });
        return completion.Task;
    }

    [SupportedOSPlatform("android29.0")]
    private void StartDeviceAuthentication(string reason)
    {
        var cancellation = new CancellationSignal();
        _deviceUnlockCancellation = cancellation;
        var builder = new Android.Hardware.Biometrics.BiometricPrompt.Builder(this)
            .SetTitle(reason);
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            builder.SetAllowedAuthenticators((int)(
                Android.Hardware.Biometrics.BiometricManagerAuthenticators.BiometricStrong |
                Android.Hardware.Biometrics.BiometricManagerAuthenticators.DeviceCredential));
        }
        else
        {
            builder.SetDeviceCredentialAllowed(true);
        }

        var prompt = builder.Build();
        prompt.Authenticate(
            cancellation,
            MainExecutor!,
            new DeviceAuthenticationCallback(this));
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (requestCode == DeviceUnlockRequestCode)
        {
            CompleteDeviceUnlock(resultCode == Result.Ok
                ? DeviceUnlockResultEnum.Success
                : DeviceUnlockResultEnum.Cancelled);
        }
    }

    private void CompleteDeviceUnlock(DeviceUnlockResultEnum result)
    {
        TaskCompletionSource<DeviceUnlockResultEnum>? completion;
        CancellationSignal? cancellation;
        lock (_deviceUnlockLock)
        {
            completion = _deviceUnlockCompletion;
            _deviceUnlockCompletion = null;
            cancellation = _deviceUnlockCancellation;
            _deviceUnlockCancellation = null;
        }

        if (result == DeviceUnlockResultEnum.Cancelled && cancellation?.IsCanceled == false)
        {
            cancellation.Cancel();
        }

        completion?.TrySetResult(result);
    }

    [SupportedOSPlatform("android29.0")]
    private sealed class DeviceAuthenticationCallback(MainActivity activity)
        : Android.Hardware.Biometrics.BiometricPrompt.AuthenticationCallback
    {
        public override void OnAuthenticationSucceeded(
            Android.Hardware.Biometrics.BiometricPrompt.AuthenticationResult? result)
        {
            activity.CompleteDeviceUnlock(DeviceUnlockResultEnum.Success);
        }

        public override void OnAuthenticationError(
            [Android.Runtime.GeneratedEnum] Android.Hardware.Biometrics.BiometricErrorCode errorCode,
            Java.Lang.ICharSequence? errString)
        {
            activity.CompleteDeviceUnlock(DeviceUnlockResultEnum.Cancelled);
        }
    }

    private static async void DispatchAuthenticationCallback(Intent? intent)
    {
        try
        {
            var callbackUri = intent?.DataString;
            var handler = IPlatformApplication.Current?.Services.GetService<AuthenticationCallbackHandler>();
            if (handler is not null && handler.CanForward(callbackUri))
            {
                await handler.ForwardAsync(callbackUri);
            }
        }
        catch (Exception exception)
        {
            AndroidLog.Warn(
                AndroidLogcatLoggerProvider.Tag,
                $"Native authentication callback dispatch failed with {exception.GetType().FullName}.");
        }
    }
}
