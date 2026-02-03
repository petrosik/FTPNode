namespace Shared
{
    public static class Utils
    {
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

        public static bool AllowedActions(this FtpItemDto ftpItem, AllowedAction action)
        {
            if ((action & AllowedAction.Read) == AllowedAction.Read ||
                (action & AllowedAction.Download) == AllowedAction.Download ||
                (action & AllowedAction.Upload) == AllowedAction.Upload ||
                (action & AllowedAction.Delete) == AllowedAction.Delete ||
                (action & AllowedAction.Rename) == AllowedAction.Rename ||
                (action & AllowedAction.ChangePermissions) == AllowedAction.ChangePermissions)
            {
                return true;
            }
            else if ((action & AllowedAction.Edit) == AllowedAction.Edit && ftpItem.Type == FileType.File)
            {
                var ext = System.IO.Path.GetExtension(ftpItem.Name).ToLower();
                if (ext == ".txt" || 
                    ext == ".html" || 
                    ext == ".htm" || 
                    ext == ".css" || 
                    ext == ".js" || 
                    ext == ".json" || 
                    ext == ".xml" || 
                    ext == ".md" || 
                    ext == ".csv" || 
                    ext == ".log" || 
                    ext == ".cfg" || 
                    ext == ".ini" || 
                    ext == ".bat" || 
                    ext == ".sh" || 
                    ext == ".py" || 
                    ext == ".java" || 
                    ext == ".c" || 
                    ext == ".cpp" || 
                    ext == ".cs")
                {
                    return true;
                }
            }
            return false;
        }

    }
}
