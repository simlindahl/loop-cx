using Microsoft.AspNetCore.Components.WebView.Maui;
using LoopCx.Features.StartRun;

namespace LoopCx;

internal static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();
        builder.Services.AddMauiBlazorWebView();

        return builder.Build();
    }
}

internal sealed class App : Application
{
    protected override Window CreateWindow(IActivationState? activationState)
    {
        var view = new BlazorWebView { HostPage = "wwwroot/index.html" };
        view.HandlerChanged += (_, _) =>
        {
            if (view.Handler?.PlatformView is global::Android.Webkit.WebView webView)
            {
                var fontScale = webView.Context?.Resources?.Configuration?.FontScale ?? 1;
                webView.Settings.TextZoom = (int)Math.Round(fontScale * 100);
            }
        };
        view.RootComponents.Add(new RootComponent
        {
            Selector = "#app",
            ComponentType = typeof(StartRunPage)
        });

        return new Window(new CorePage
        {
            Content = view,
            SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.Container),
            BackgroundColor = Color.FromArgb("#111111")
        });
    }
}


internal sealed class CorePage : ContentPage
{
    protected override bool OnBackButtonPressed()
    {
        if (CoreDialogNavigation.Close is not { } close) return base.OnBackButtonPressed();

        close();

        return true;
    }
}
