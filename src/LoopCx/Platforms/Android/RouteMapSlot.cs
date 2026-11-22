using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace LoopCx;

internal sealed class RouteMapSlot<T> : IAsyncDisposable where T : class
{
    private readonly IJSObjectReference module;
    private readonly IJSObjectReference observer;
    private readonly DotNetObjectReference<T> callback;

    private RouteMapSlot(IJSObjectReference module, IJSObjectReference observer, DotNetObjectReference<T> callback)
    {
        this.module = module;
        this.observer = observer;
        this.callback = callback;
    }

    public static async Task<RouteMapSlot<T>> Observe(IJSRuntime js, ElementReference slot, T receiver, Guid owner)
    {
        var callback = DotNetObjectReference.Create(receiver);
        IJSObjectReference? module = null;

        try
        {
            module = await js.InvokeAsync<IJSObjectReference>("import", "./js/history-map-slot.js");
            var observer = await module.InvokeAsync<IJSObjectReference>("observe", slot, callback, owner);
            return new(module, observer, callback);
        }
        catch (Exception error) when (error is JSException or OperationCanceledException)
        {
            callback.Dispose();
            if (module is not null) await Release(module);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await observer.InvokeVoidAsync("dispose");
        }
        catch (Exception error) when (error is JSException or OperationCanceledException)
        {
            // The owning WebView may already be gone.
        }
        finally
        {
            await Release(observer);
            await Release(module);
            callback.Dispose();
        }
    }

    private static async ValueTask Release(IJSObjectReference reference)
    {
        try
        {
            await reference.DisposeAsync();
        }
        catch (Exception error) when (error is JSException or OperationCanceledException)
        {
            // Disposal stops at the already removed JS ownership boundary.
        }
    }
}
