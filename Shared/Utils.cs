namespace Shared
{
    public static class Utils
    {
        public static UnixPermission GetPermissions(int chmod, PermissionScope scope = PermissionScope.Owner)
        {
            // Convert only if it looks like an octal-text int
            if (chmod > 511) // real bitmasks never exceed 0777 (511 decimal)
                chmod = Convert.ToInt32(chmod.ToString(), 8);

            int shift = scope switch
            {
                PermissionScope.Owner => 6,
                PermissionScope.Group => 3,
                PermissionScope.Others => 0,
                _ => 0
            };

            int bits = (chmod >> shift) & 0b111;
            UnixPermission result = UnixPermission.None;

            if ((bits & 4) != 0) result |= UnixPermission.Read;
            if ((bits & 2) != 0) result |= UnixPermission.Write;
            if ((bits & 1) != 0) result |= UnixPermission.Execute;

            return result;
        }
        public static string GetUnixPermissions(int chmod)
        {
            // chmod is usually octal: e.g., 755, 644
            // Convert to string in octal
            string octal = chmod.ToString("D3"); // ensures 3 digits

            char[] result = new char[9];
            for (int i = 0; i < 3; i++)
            {
                int digit = octal[i] - '0'; // convert char to int
                result[i * 3 + 0] = (digit & 4) != 0 ? 'r' : '-';
                result[i * 3 + 1] = (digit & 2) != 0 ? 'w' : '-';
                result[i * 3 + 2] = (digit & 1) != 0 ? 'x' : '-';
            }

            return new string(result); // e.g., "rwxr-xr-x"
        }

        public static double ToKB(this long bytes) => bytes / 1024.0;
        public static double ToMB(this long bytes) => bytes / (1024.0 * 1024.0);
        public static double ToGB(this long bytes) => bytes / (1024.0 * 1024.0 * 1024.0);
        public static string ToClosestSize(this long bytes)
        {
            if (bytes < 0)
                throw new ArgumentOutOfRangeException(nameof(bytes));

            string[] units = { "B", "KB", "MB", "GB", "TB", "PB" };
            double size = bytes;
            int unitIndex = 0;

            while (size >= 1024 && unitIndex < units.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }

            return $"{size:0.##} {units[unitIndex]}";
        }

        public static bool AllowedActions(this FtpItemDto ftpItem, AllowedAction action, long sizeLimit = 1048576)
        {
            var perms = GetPermissions(ftpItem.Permissions, PermissionScope.Owner);
            if (((action & AllowedAction.Read) == AllowedAction.Read || (action & AllowedAction.Download) == AllowedAction.Download) && (perms & UnixPermission.Read) == UnixPermission.Read)
            {
                return true;
            }
            else if (
                (action & AllowedAction.Upload) == AllowedAction.Upload ||
                (action & AllowedAction.Delete) == AllowedAction.Delete ||
                (action & AllowedAction.Rename) == AllowedAction.Rename)
            {
                return true;
            }
            else if (((action & AllowedAction.ChangePermissions) == AllowedAction.ChangePermissions) && (perms & UnixPermission.Write) == UnixPermission.Write)
            {
                return true;
            }
            else if ((action & AllowedAction.Edit) == AllowedAction.Edit && ftpItem.Type != FileType.Directory && ftpItem.Size <=sizeLimit && (perms & UnixPermission.Write) == UnixPermission.Write)
            {
                var ext = System.IO.Path.GetExtension(ftpItem.Name).ToLower();
                if (ext == ".txt"  || 
                    ext == ".html" || 
                    ext == ".htm"  || 
                    ext == ".css"  || 
                    ext == ".js"   || 
                    ext == ".json" || 
                    ext == ".xml"  || 
                    ext == ".md"   || 
                    ext == ".csv"  || 
                    ext == ".log"  || 
                    ext == ".cfg"  || 
                    ext == ".ini"  || 
                    ext == ".bat"  || 
                    ext == ".sh"   || 
                    ext == ".py"   || 
                    ext == ".java" || 
                    ext == ".c"    || 
                    ext == ".cpp"  || 
                    ext == ".cs")
                {
                    return true;
                }
            }
            return false;
        }
        public static string ToSaneString(this DateTime time)
        {
            return time.ToString("yyyy/MM/dd HH:mm:ss");
        }
        public static int CalcPerm(bool r, bool w, bool x) => (r ? 4 : 0) + (w ? 2 : 0) + (x ? 1 : 0);
    }
}
