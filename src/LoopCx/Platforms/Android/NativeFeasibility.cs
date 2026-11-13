#if DEBUG
using Android.Bluetooth;
using Android.Bluetooth.LE;
using Android.Companion;
using Android.Content;
using Android.Content.PM;
using Android.Media;
using Android.OS;
using Android.Speech.Tts;
using TextToSpeech = Android.Speech.Tts.TextToSpeech;
using System.Runtime.Versioning;

namespace LoopCx;

// Explicit developer probes; no UI readouts, exporter or persisted health data.
[SupportedOSPlatform("android33.0")]
internal sealed class NativeFeasibility : Java.Lang.Object
{
    private readonly RunService owner;
    private readonly Handler main = new(Looper.MainLooper!);
    private BluetoothGatt? gatt;
    private BluetoothDevice? selected;
    private int generation;
    private int ownedAssociation = -1;
    private bool subscribed;
    private int retries;
    private Action? reconnect;
    private Action? stale;
    private TextToSpeech? speech;
    private AudioManager? audio;
    private AudioFocusRequestClass? focus;
    private bool speechRequested;
    private int speechGeneration;
    private bool disposed;
    private static readonly Java.Util.UUID ServiceId = Java.Util.UUID.FromString("0000180d-0000-1000-8000-00805f9b34fb")!;
    private static readonly Java.Util.UUID MeasurementId = Java.Util.UUID.FromString("00002a37-0000-1000-8000-00805f9b34fb")!;
    private static readonly Java.Util.UUID CccId = Java.Util.UUID.FromString("00002902-0000-1000-8000-00805f9b34fb")!;

    internal NativeFeasibility(RunService owner) => this.owner = owner;

    internal static void Report(string state) => global::Android.Util.Log.Info("LoopNative", state);

    internal void Connect(string address, int associationId)
    {
        StopBluetooth();
        ownedAssociation = associationId;
        if (owner.CheckSelfPermission(global::Android.Manifest.Permission.BluetoothConnect) != Permission.Granted)
        {
            Report("ble:permission-denied;heart-rate=unknown");
            return;
        }

        var adapter = ((BluetoothManager?)owner.GetSystemService(Context.BluetoothService))?.Adapter;
        if (adapter is null || !adapter.IsEnabled)
        {
            Report("ble:unavailable;heart-rate=unknown");
            return;
        }

        try
        {
            var normalized = address.ToUpperInvariant();
            if (!BluetoothAdapter.CheckBluetoothAddress(normalized))
            {
                StopBluetooth();
                Report("ble:invalid-selected-address;heart-rate=unknown");
                return;
            }
            selected = adapter.GetRemoteDevice(normalized);
            retries = 0;
            OpenGatt();
        }
        catch (Java.Lang.RuntimeException)
        {
            StopBluetooth();
            Report("ble:connect-unavailable;heart-rate=unknown");
        }
    }

    private void OpenGatt()
    {
        if (disposed || selected is null) return;
        subscribed = false;
        var token = ++generation;
        gatt = selected.ConnectGatt(owner, false, new Gatt(this, token), BluetoothTransports.Le);
        main.PostDelayed(() =>
        {
            if (!disposed && token == generation && !subscribed) Disconnected();
        }, 15000);
        Report("ble:connecting;heart-rate=unknown");
    }

    private void Dispatch(int token, Action action) => main.Post(() =>
    {
        if (!disposed && token == generation)
        {
            try { action(); }
            catch (Java.Lang.SecurityException) { StopBluetooth(); Report("ble:permission-revoked;heart-rate=unknown"); }
            catch (Java.Lang.IllegalStateException) { StopBluetooth(); Report("ble:adapter-unavailable;heart-rate=unknown"); }
        }
    });

    private void Disconnected()
    {
        try { gatt?.Close(); }
        catch (Java.Lang.SecurityException) { Report("ble:close-permission-revoked"); }
        gatt = null;
        if (stale is not null) main.RemoveCallbacks(stale);
        Report("ble:disconnected;heart-rate=unknown");
        generation++;
        if (disposed || retries++ >= 2) return;
        var token = generation;
        reconnect = () => Dispatch(token, OpenGatt);
        main.PostDelayed(reconnect, 2000);
    }

    private void Measurement(byte[] value)
    {
        var bpm = Features.ActiveRun.HeartRateMeasurement.Parse(value);

        Report(bpm is { } heartRate ? $"ble:observation;heart-rate={heartRate}" : "ble:contact-or-payload-invalid;heart-rate=unknown");
        if (stale is not null) main.RemoveCallbacks(stale);
        stale = () => Report("ble:stale;heart-rate=unknown");
        main.PostDelayed(stale, 5000);
    }

    internal void SpeakOnce()
    {
        if (speechRequested || disposed) return;
        speechRequested = true;
        var token = ++speechGeneration;
        speech = new TextToSpeech(owner, new SpeechInit(this, token));
        main.PostDelayed(() =>
        {
            if (token == speechGeneration) { Report("speech:timeout"); StopSpeech(); }
        }, 15000);
    }

    private void Initialized(int token, OperationResult status) => main.Post(() =>
    {
        if (disposed || !speechRequested || token != speechGeneration) return;
        if (status != OperationResult.Success || speech is null ||
            speech.SetLanguage(Java.Util.Locale.Us) is LanguageAvailableResult.MissingData or LanguageAvailableResult.NotSupported)
        {
            Report("speech:language-or-engine-unavailable");
            StopSpeech();
            return;
        }

        audio = (AudioManager?)owner.GetSystemService(Context.AudioService);
        var attributes = new AudioAttributes.Builder()!.SetUsage(AudioUsageKind.AssistanceNavigationGuidance)!
            .SetContentType(AudioContentType.Speech)!.Build()!;
        focus = new AudioFocusRequestClass.Builder(AudioFocus.GainTransientMayDuck)
            .SetAudioAttributes(attributes)!.SetOnAudioFocusChangeListener(new Focus(this, token), main)!.Build()!;
        if (audio?.RequestAudioFocus(focus) != AudioFocusRequest.Granted)
        {
            Report("speech:focus-denied");
            StopSpeech();
            return;
        }

        speech.SetAudioAttributes(attributes);
        speech.SetOnUtteranceProgressListener(new SpeechProgress(this, token));
        if (speech.Speak("LOOP audio test", QueueMode.Flush, null, "loop-native-probe") == OperationResult.Error)
        {
            Report("speech:speak-error");
            StopSpeech();
        }
    });

    private void StopSpeech()
    {
        speechGeneration++;
        speech?.Stop();
        speech?.Shutdown();
        speech = null;
        if (audio is not null && focus is not null) audio.AbandonAudioFocusRequest(focus);
        focus = null;
        speechRequested = false;
    }

    private void StopBluetooth()
    {
        generation++;
        if (reconnect is not null) main.RemoveCallbacks(reconnect);
        if (stale is not null) main.RemoveCallbacks(stale);
        try { gatt?.Disconnect(); }
        catch (Java.Lang.SecurityException) { Report("ble:disconnect-permission-revoked"); }
        try { gatt?.Close(); }
        catch (Java.Lang.SecurityException) { Report("ble:close-permission-revoked"); }
        gatt = null;
        selected = null;
        if (ownedAssociation >= 0)
        {
            try { ((CompanionDeviceManager?)owner.GetSystemService(Context.CompanionDeviceService))?.Disassociate(ownedAssociation); }
            catch (Java.Lang.RuntimeException) { Report("ble:own-association-cleanup-failed"); }
            ownedAssociation = -1;
        }
        Report("ble:stopped;heart-rate=unknown");
    }

    internal void Stop()
    {
        disposed = true;
        StopBluetooth();
        StopSpeech();
    }

    private sealed class Gatt(NativeFeasibility probe, int token) : BluetoothGattCallback
    {
        public override void OnConnectionStateChange(BluetoothGatt? gatt, GattStatus status, ProfileState newState) => probe.Dispatch(token, () =>
        {
            if (status == GattStatus.Success && newState == ProfileState.Connected) gatt?.DiscoverServices();
            else if (newState == ProfileState.Disconnected || status != GattStatus.Success) probe.Disconnected();
        });

        public override void OnServicesDiscovered(BluetoothGatt? gatt, GattStatus status) => probe.Dispatch(token, () =>
        {
            var characteristic = gatt?.GetService(ServiceId)?.GetCharacteristic(MeasurementId);
            var ccc = characteristic?.GetDescriptor(CccId);
            if (status != GattStatus.Success || characteristic is null || ccc is null ||
                (characteristic.Properties & GattProperty.Notify) == 0 || !gatt!.SetCharacteristicNotification(characteristic, true))
            {
                Report("ble:measurement-notify-unavailable;heart-rate=unknown");
                probe.StopBluetooth();
                return;
            }

            if (gatt.WriteDescriptor(ccc, [1, 0]) != (int)CurrentBluetoothStatusCodes.Success)
                probe.Disconnected();
        });

        public override void OnDescriptorWrite(BluetoothGatt? gatt, BluetoothGattDescriptor? descriptor, GattStatus status) => probe.Dispatch(token, () =>
        {
            if (status == GattStatus.Success) { probe.subscribed = true; Report("ble:subscribed;heart-rate=unknown"); }
            else probe.Disconnected();
        });

        public override void OnCharacteristicChanged(BluetoothGatt gatt, BluetoothGattCharacteristic characteristic, byte[] value)
        {
            var copy = value.ToArray();
            probe.Dispatch(token, () => probe.Measurement(copy));
        }
    }

    private sealed class Focus(NativeFeasibility probe, int token) : Java.Lang.Object, AudioManager.IOnAudioFocusChangeListener
    {
        public void OnAudioFocusChange(AudioFocus focusChange)
        {
            if (focusChange < 0) probe.main.Post(() => { if (token == probe.speechGeneration) probe.StopSpeech(); });
        }
    }

    private sealed class SpeechInit(NativeFeasibility probe, int token) : Java.Lang.Object, TextToSpeech.IOnInitListener
    {
        public void OnInit(OperationResult status) => probe.Initialized(token, status);
    }

    private sealed class SpeechProgress(NativeFeasibility probe, int token) : UtteranceProgressListener
    {
        public override void OnStart(string? utteranceId) => probe.main.Post(() => { if (token == probe.speechGeneration) Report("speech:started"); });
        public override void OnDone(string? utteranceId) => probe.main.Post(() => { if (token == probe.speechGeneration) { Report("speech:completed"); probe.StopSpeech(); } });
        [Obsolete("Required abstract legacy callback; modern error callback is also handled.")]
        public override void OnError(string? utteranceId) => probe.main.Post(() => { if (token == probe.speechGeneration) { Report("speech:error"); probe.StopSpeech(); } });
    }
}
#endif
