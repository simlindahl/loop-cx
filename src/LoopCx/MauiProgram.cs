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
        view.RootComponents.Add(new RootComponent
        {
            Selector = "#app",
            ComponentType = typeof(StartRunPage)
        });
        return new Window(new ContentPage { Content = view });
    }
}
