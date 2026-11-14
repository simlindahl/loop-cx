using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace LoopCx;

internal static class RunJsonShareSheet
{
    internal static Task OpenAsync(string path) => MainThread.InvokeOnMainThreadAsync(() =>
        Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = "Share workout data",
            File = new ShareFile(path, "application/json")
        }));
}
