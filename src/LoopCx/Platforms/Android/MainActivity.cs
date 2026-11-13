using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace LoopCx;

[Activity(Name = "se.loopcx.running.MainActivity", Theme = "@style/Maui.MainTheme.NoActionBar",
    MainLauncher = true, Exported = true, LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation |
        ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize |
        ConfigChanges.Density)]
public sealed class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        _ = RunJournal.Shared;
#if DEBUG
        HandleDebugCommand(Intent);
#endif
    }

#if DEBUG
    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        HandleDebugCommand(intent);
    }

    private static void HandleDebugCommand(Intent? intent)
    {
        var command = intent?.GetStringExtra("loop-command");
        if (command is "start" or "start-c2" or "pause" or "resume" or "extend" or "skip" or "complete")
            RunService.Command(command);
    }
#endif
}
