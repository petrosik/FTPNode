namespace Shared
{
    public enum FileType
    {
        Directory,
        File,
        Link,
        Unknown
    }
    [Flags]
    public enum AllowedAction
    {
        None = 0,
        Read = 1,
        Delete = 2,
        Upload = 4,
        Download = 8,
        ChangePermissions = 16,
        Rename = 32,
        Edit = 64
    }
    [Flags]
    public enum UnixPermission
    {
        None = 0,
        Read = 1,
        Write = 2,
        Execute = 4
    }
    public enum PermissionScope
    {
        Owner,
        Group,
        Others
    }
}
