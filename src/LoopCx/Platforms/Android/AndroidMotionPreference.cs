using Android.Content;
using Android.Database;
using Android.OS;
using Android.Provider;

namespace LoopCx;

// Read-only system accessibility preference. The application resolver never retains an Activity.
internal sealed class AndroidMotionPreference : ContentObserver
{
    private readonly ContentResolver resolver;
    private bool registered;
    internal static event Action? Changed;

    internal AndroidMotionPreference(ContentResolver resolver) : base(new Handler(Looper.MainLooper!))
    {
        this.resolver = resolver;

        try
        {
            using var uri = Settings.Global.GetUriFor(Settings.Global.AnimatorDurationScale);
            if (uri is not null)
            {
                resolver.RegisterContentObserver(uri, false, this);
                registered = true;
            }
        }
        catch (Java.Lang.RuntimeException) { /* Resume synchronization remains available. */ }

        Refresh();
    }

    internal static bool? Current
    {
        get
        {
            try
            {
                var resolver = global::Android.App.Application.Context.ContentResolver;

                return resolver is null ? null : Settings.Global.GetFloat(resolver, Settings.Global.AnimatorDurationScale, 1) <= 0;
            }
            catch (Java.Lang.RuntimeException) { return null; }
        }
    }

    internal static void Refresh() => Changed?.Invoke();

    public override void OnChange(bool selfChange) => Refresh();

    protected override void Dispose(bool disposing)
    {
        if (disposing && registered)
        {
            registered = false;
            try { resolver.UnregisterContentObserver(this); }
            catch (Java.Lang.RuntimeException) { }
        }

        base.Dispose(disposing);
    }
}
