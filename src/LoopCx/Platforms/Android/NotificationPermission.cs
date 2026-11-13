using System.Runtime.Versioning;

namespace LoopCx;

[SupportedOSPlatform("android33.0")]
public sealed class NotificationPermission : Permissions.BasePlatformPermission
{
    public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
        [(global::Android.Manifest.Permission.PostNotifications, true)];
}
