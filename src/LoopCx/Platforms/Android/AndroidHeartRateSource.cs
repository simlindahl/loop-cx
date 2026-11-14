using Android.Bluetooth;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using LoopCx.Features.ActiveRun;

namespace LoopCx;

// No scan, association, permission prompt or health probe: connects only the explicitly selected source.
internal sealed class AndroidHeartRateSource : Java.Lang.Object
{
    private readonly Context context;
    private readonly Handler handler;
    private readonly Action<HeartRateObservation> deliver;
    private readonly TimeSpan maximumGap;
    private BluetoothGatt? gatt;
    private BluetoothDevice? selected;
    private int generation;
    private int retries;
    private bool stopped;
    private bool subscribed;
    private Action? stale;
    private Action? retry;
    private static readonly Java.Util.UUID ServiceId = Java.Util.UUID.FromString("0000180d-0000-1000-8000-00805f9b34fb")!;
    private static readonly Java.Util.UUID MeasurementId = Java.Util.UUID.FromString("00002a37-0000-1000-8000-00805f9b34fb")!;
    private static readonly Java.Util.UUID CccId = Java.Util.UUID.FromString("00002902-0000-1000-8000-00805f9b34fb")!;

    internal AndroidHeartRateSource(Context context, Handler handler, HeartRateConfiguration configuration, Action<HeartRateObservation> deliver)
    {
        this.context = context;
        this.handler = handler;
        this.deliver = deliver;
        maximumGap = configuration.Policy.MaximumGap;
    }

    internal void Connect(string address)
    {
        try
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(31) && context.CheckSelfPermission(global::Android.Manifest.Permission.BluetoothConnect) != Permission.Granted)
            {
                Emit(null, "permission-denied");
                return;
            }

            var adapter = ((BluetoothManager?)context.GetSystemService(Context.BluetoothService))?.Adapter;
            var normalized = address.ToUpperInvariant();

            if (adapter is null || !adapter.IsEnabled || !BluetoothAdapter.CheckBluetoothAddress(normalized))
            {
                Emit(null, "unavailable");
                return;
            }

            selected = adapter.GetRemoteDevice(normalized);
            Open();
        }
        catch (Java.Lang.RuntimeException) { Fail("connect-failed"); }
    }

    private void Open()
    {
        if (stopped || selected is null) return;

        subscribed = false;
        var token = ++generation;
        gatt = selected.ConnectGatt(context, false, new Gatt(this, token), BluetoothTransports.Le);
        handler.PostDelayed(() =>
        {
            if (!stopped && token == generation && !subscribed) Disconnected();
        }, 15000);
        Emit(null, "connecting");
    }

    private void Dispatch(int token, Action action) => handler.Post(() =>
    {
        if (stopped || token != generation) return;

        try { action(); }
        catch (Java.Lang.RuntimeException) { Fail("permission-or-adapter-unavailable"); }
    });

    private void Emit(int? bpm, string quality) => deliver(new(TimeSpan.FromTicks(SystemClock.ElapsedRealtimeNanos() / 100), DateTimeOffset.UtcNow, bpm, quality));

    private void Measurement(byte[] payload, TimeSpan receipt, DateTimeOffset utc)
    {
        var bpm = HeartRateMeasurement.Parse(payload);
        deliver(new(receipt, utc, bpm, bpm is null ? "contact-or-payload-invalid" : "valid"));

        if (stale is not null) handler.RemoveCallbacks(stale);

        var token = generation;
        stale = () => { if (!stopped && token == generation) Emit(null, "stale"); };
        handler.PostDelayed(stale, (long)maximumGap.TotalMilliseconds + 1);
    }

    private void CloseGatt()
    {
        generation++;
        if (stale is not null) handler.RemoveCallbacks(stale);

        try { gatt?.Disconnect(); }
        catch (Java.Lang.RuntimeException) { }
        try { gatt?.Close(); }
        catch (Java.Lang.RuntimeException) { }
        gatt = null;
    }

    private void Disconnected()
    {
        CloseGatt();
        Emit(null, "disconnected");

        if (stopped || retries++ >= 3) return;

        var token = generation;
        retry = () => Dispatch(token, Open);
        handler.PostDelayed(retry, 2000);
    }

    private void Fail(string quality)
    {
        CloseGatt();
        Emit(null, quality);
    }

    internal void Refresh(string address)
    {
        if (stopped || gatt is not null) return;

        if (retry is not null) handler.RemoveCallbacks(retry);
        retries = 0;
        Connect(address);
    }

    internal void Stop()
    {
        stopped = true;
        if (retry is not null) handler.RemoveCallbacks(retry);
        CloseGatt();
        selected = null;
    }

    private sealed class Gatt(AndroidHeartRateSource source, int token) : BluetoothGattCallback
    {
        public override void OnConnectionStateChange(BluetoothGatt? gatt, GattStatus status, ProfileState newState) => source.Dispatch(token, () =>
        {
            if (status == GattStatus.Success && newState == ProfileState.Connected) gatt?.DiscoverServices();
            else if (newState == ProfileState.Disconnected || status != GattStatus.Success) source.Disconnected();
        });

        public override void OnServicesDiscovered(BluetoothGatt? gatt, GattStatus status) => source.Dispatch(token, () =>
        {
            var characteristic = gatt?.GetService(ServiceId)?.GetCharacteristic(MeasurementId);
            var ccc = characteristic?.GetDescriptor(CccId);

            if (status != GattStatus.Success || characteristic is null || ccc is null ||
                (characteristic.Properties & GattProperty.Notify) == 0 || !gatt!.SetCharacteristicNotification(characteristic, true))
            {
                source.Fail("notify-unavailable");
                return;
            }

            if (OperatingSystem.IsAndroidVersionAtLeast(33))
            {
                if (gatt.WriteDescriptor(ccc, [1, 0]) != (int)CurrentBluetoothStatusCodes.Success) source.Disconnected();
            }
            else
            {
                ccc.SetValue([1, 0]);
                if (!gatt.WriteDescriptor(ccc)) source.Disconnected();
            }
        });

        public override void OnDescriptorWrite(BluetoothGatt? gatt, BluetoothGattDescriptor? descriptor, GattStatus status) => source.Dispatch(token, () =>
        {
            if (status == GattStatus.Success && descriptor?.Uuid?.Equals(CccId) == true) { source.subscribed = true; source.retries = 0; }
            else source.Disconnected();
        });

        public override void OnCharacteristicChanged(BluetoothGatt? gatt, BluetoothGattCharacteristic? characteristic)
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(33) || characteristic?.Uuid?.Equals(MeasurementId) != true) return;

            var receipt = TimeSpan.FromTicks(SystemClock.ElapsedRealtimeNanos() / 100);
            var utc = DateTimeOffset.UtcNow;
            var copy = characteristic.GetValue()?.ToArray() ?? [];
            source.Dispatch(token, () => source.Measurement(copy, receipt, utc));
        }

        public override void OnCharacteristicChanged(BluetoothGatt gatt, BluetoothGattCharacteristic characteristic, byte[] value)
        {
            if (characteristic.Uuid?.Equals(MeasurementId) != true) return;

            // Capture clocks at the native callback, before the service-handler queue can delay processing.
            var receipt = TimeSpan.FromTicks(SystemClock.ElapsedRealtimeNanos() / 100);
            var utc = DateTimeOffset.UtcNow;
            var copy = value.ToArray();
            source.Dispatch(token, () => source.Measurement(copy, receipt, utc));
        }
    }
}
