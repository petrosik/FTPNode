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
        Unknown = -1,
        Read = 0,
        Delete = 1,
        Upload = 2,
        Download = 4,
        ChangePermissions = 8,
        Rename = 16,
        Edit = 32,
         
    }
}
