using Android.Content;
using Android.Media;
using Android.OS;
using Android.Speech.Tts;
using LoopCx.Features.ActiveRun;
using TextToSpeech = Android.Speech.Tts.TextToSpeech;

namespace LoopCx;

// One run-owned utterance: no shared MAUI queue or music playback commands.
internal sealed class AndroidRunAudio : IDisposable
{
    private readonly Handler main;
    private readonly Guid runId;
    private TextToSpeech? engine;
    private AudioManager? manager;
    private AudioFocusRequestClass? focus;
    private Action? watchdog;
    private int generation;
    private bool disposed;
    private AudioCueKind? currentKind;
    private Func<bool>? stillCurrent;

    public AndroidRunAudio(Handler handler, Guid id) { main = handler; runId = id; }
    private string Utterance(int token) => $"{runId:N}:{token}";

    public bool Speak(string text, AudioCueKind kind, Func<bool>? validity = null)
    {
        if (disposed || !AudioPriority.CanReplace(kind, currentKind) || validity?.Invoke() == false) return false;
        Cancel();
        currentKind = kind;
        stillCurrent = validity;
        var token = generation;
        var received = SystemClock.ElapsedRealtime();
        try
        {
            engine = new TextToSpeech(global::Android.App.Application.Context, new Init(this, token, received, text));
            // Cleanup ceiling only; actual callbacks, phase preemption and focus loss normally clean up.
            watchdog = () => { if (token == generation) { Report("callback-timeout", token); Cancel(); } };
            main.PostDelayed(watchdog, (long)(Math.Max(30, text.Length / 5d) * 1000));
            Report("requested", token, text);
        }
        catch (Java.Lang.RuntimeException) { Cancel(); }
        return true;
    }

    private void Initialized(int token, long received, string text, OperationResult result) => main.Post(() =>
    {
        if (disposed || token != generation) return;
        if (!ValidateCurrent()) return;
        try
        {
            if (engine is null || result != OperationResult.Success || SystemClock.ElapsedRealtime() - received > 3000)
            { Report("engine-or-language-unavailable", token); Cancel(); return; }
            var voice = engine.Voices?.Where(voice => !voice.IsNetworkConnectionRequired && voice.Locale?.Language == "en"
                    && voice.Features is { } features && !features.Contains(TextToSpeech.Engine.KeyFeatureNotInstalled))
                .OrderBy(voice => voice.Name, StringComparer.Ordinal).FirstOrDefault();
            if (voice is null || engine.SetVoice(voice) == OperationResult.Error)
            { Report("offline-english-voice-unavailable", token); Cancel(); return; }
            manager = global::Android.App.Application.Context.GetSystemService(Context.AudioService) as AudioManager;
            if (manager is null) { Cancel(); return; }
            var attributes = new AudioAttributes.Builder().SetUsage(AudioUsageKind.AssistanceNavigationGuidance)!
                .SetContentType(AudioContentType.Speech)!.Build()!;
            focus = new AudioFocusRequestClass.Builder(AudioFocus.GainTransientMayDuck).SetAudioAttributes(attributes)!
                .SetOnAudioFocusChangeListener(new Focus(this, token), main)!.Build();
            if (manager.RequestAudioFocus(focus!) != AudioFocusRequest.Granted) { Report("focus-denied", token); Cancel(); return; }
            engine.SetAudioAttributes(attributes);
            engine.SetOnUtteranceProgressListener(new Progress(this, token));
            if (!ValidateCurrent()) return;
            if (engine.Speak(text, QueueMode.Flush, null, Utterance(token)) == OperationResult.Error)
            { Report("speak-error", token); Cancel(); }
        }
        catch (Java.Lang.RuntimeException) { Cancel(); }
    });

    private void Completed(int token, string? utterance, string outcome) => main.Post(() =>
    {
        if (token != generation || utterance != Utterance(token)) return;
        Report(outcome, token);
        Cancel();
    });

    private sealed class Init(AndroidRunAudio owner, int token, long received, string text) : Java.Lang.Object, TextToSpeech.IOnInitListener
    {
        public void OnInit(OperationResult status) => owner.Initialized(token, received, text, status);
    }
    private sealed class Focus(AndroidRunAudio owner, int token) : Java.Lang.Object, AudioManager.IOnAudioFocusChangeListener
    {
        public void OnAudioFocusChange(AudioFocus change) => owner.main.Post(() =>
        { if (token == owner.generation && change != AudioFocus.Gain) owner.Cancel(); });
    }
    private sealed class Progress(AndroidRunAudio owner, int token) : UtteranceProgressListener
    {
        public override void OnStart(string? utteranceId) => owner.main.Post(() =>
        {
            if (token == owner.generation && utteranceId == owner.Utterance(token) && owner.ValidateCurrent())
                owner.Report("started", token);
        });
        public override void OnDone(string? utteranceId) => owner.Completed(token, utteranceId, "completed");
        [Obsolete("Required abstract legacy callback; native errors use the same generation guard.")]
        public override void OnError(string? utteranceId) => owner.Completed(token, utteranceId, "error");
        public override void OnStop(string? utteranceId, bool interrupted) => owner.Completed(token, utteranceId, "stopped");
    }

    private void Report(string outcome, int token, string? text = null)
    {
#if DEBUG
        var textHash = text is null ? null : Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));
        global::Android.Util.Log.Info("LoopAudio", System.Text.Json.JsonSerializer.Serialize(new { RunId = runId, Generation = token, Outcome = outcome, TextSha256 = textHash, TextLength = text?.Length }));
#endif
    }

    public bool ValidateCurrent()
    {
        if (stillCurrent?.Invoke() != false) return true;
        Cancel();
        return false;
    }

    public void Cancel()
    {
        currentKind = null;
        stillCurrent = null;
        generation++;
        if (watchdog is not null) main.RemoveCallbacks(watchdog);
        watchdog = null;
        try { engine?.Stop(); engine?.Shutdown(); }
        catch (Java.Lang.RuntimeException) { }
        engine?.Dispose(); engine = null;
        if (focus is not null)
        {
            try { manager?.AbandonAudioFocusRequest(focus); }
            catch (Java.Lang.RuntimeException) { }
            focus.Dispose(); focus = null;
        }
    }

    public void Dispose() { disposed = true; Cancel(); }
}
