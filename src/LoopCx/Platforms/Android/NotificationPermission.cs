namespace LoopCx;

public sealed class NotificationPermission : Permissions.BasePlatformPermission
{
    public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
        [(global::Android.Manifest.Permission.PostNotifications, true)];
}
