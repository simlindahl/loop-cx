using Android.App;
using Android.OS;
using Android.Webkit;
using Android.Views;
using View = Android.Views.View;

namespace LoopCx;

[Activity(Name = "se.loopcx.running.HealthPrivacyActivity", Exported = false,
    Theme = "@android:style/Theme.Material.NoActionBar")]
public sealed class HealthPrivacyActivity : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        var view = new global::Android.Webkit.WebView(this);
        view.Settings.JavaScriptEnabled = true;
        view.Settings.BlockNetworkLoads = true;
        view.Settings.TextZoom = (int)Math.Round(100 * Resources!.Configuration!.FontScale);
        view.SetBackgroundColor(global::Android.Graphics.Color.Rgb(17, 17, 17));
        view.SetWebViewClient(new LocalPage(this));
        // Insets belong to the outer layout: a scrolling WebView must not draw under system bars.
        var container = new global::Android.Widget.FrameLayout(this);
        container.SetClipToPadding(true);
        container.AddView(view, new global::Android.Widget.FrameLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
        container.SetOnApplyWindowInsetsListener(new Insets());
        SetContentView(container);
        view.LoadUrl("file:///android_asset/wwwroot/health-connect-privacy.html");
    }

    private sealed class LocalPage(Activity owner) : WebViewClient
    {
        public override bool ShouldOverrideUrlLoading(global::Android.Webkit.WebView? view, IWebResourceRequest? request)
        {
            if (request?.Url?.Scheme == "loop-privacy") owner.Finish();
            return true; // Offline document; no outgoing navigation.
        }
    }

    private sealed class Insets : Java.Lang.Object, View.IOnApplyWindowInsetsListener
    {
        public WindowInsets OnApplyWindowInsets(View view, WindowInsets insets)
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                var bars = insets.GetInsets(WindowInsets.Type.SystemBars() | WindowInsets.Type.DisplayCutout());
                if (bars is not null) view?.SetPadding(bars.Left, bars.Top, bars.Right, bars.Bottom);
            }
            return insets;
        }
    }
}
