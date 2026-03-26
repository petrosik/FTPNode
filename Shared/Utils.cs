using System.Text.Json;

namespace Shared
{
    public static class Utils
    {
        private static readonly HashSet<string> ImageExtensions = new()
        {
            ".png", ".jpg", ".jpeg", ".gif",
            ".bmp", ".webp", ".tiff", ".tif",
            ".ico", ".heic", ".heif", ".svg"
        };
        private static readonly HashSet<string> TextLikeExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".html", ".htm", ".css",
            ".js", ".json", ".xml", ".md",
            ".csv", ".log", ".cfg", ".ini",
            ".bat", ".sh", ".py", ".java",
            ".yml", ".yaml", ".c", ".cpp",
            ".cs", ".rb", ".php", ".ts",
            ".swift", ".go", ".rs", ".ps1",
            ".vbs", ".toml", ".properties", ".env",
            ".make", "Makefile", ".dockerfile", ".conf",
            ".tex", ".rst", ".adoc",".out",
            ".msg"
        };
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
        public static string ToClosestSize(this long bytes, string? overrideunits = null)
        {
            if (bytes < 0)
                throw new ArgumentOutOfRangeException(nameof(bytes));

            string[] units = ["B", "KB", "MB", "GB", "TB", "PB"];
            if (!string.IsNullOrWhiteSpace(overrideunits))
            {
                try
                {
                    units = JsonSerializer.Deserialize<string[]>(overrideunits) ?? units;
                }
                catch { }
            }
            double size = bytes;
            int unitIndex = 0;

            while (size >= 1024 && unitIndex < units.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }

            return $"{size:0.##} {units[unitIndex]}";
        }

        public static bool AllowedActions(this FtpItemDto ftpItem, AllowedAction action, out bool text, long sizeLimit = 1048576)
        {
            text = false;
            var perms = GetPermissions(ftpItem.Permissions, PermissionScope.Owner);
            if ((action & AllowedAction.Download) == AllowedAction.Download && ftpItem.Type != FileType.Directory && (perms & UnixPermission.Read) == UnixPermission.Read)
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
            else if ((((action & AllowedAction.Read) == AllowedAction.Read) && (perms & UnixPermission.Read) == UnixPermission.Read) ||
                ((action & AllowedAction.Edit) == AllowedAction.Edit && ftpItem.Type != FileType.Directory && ftpItem.Size <= sizeLimit && (perms & UnixPermission.Write) == UnixPermission.Write))
            {
                var ext = System.IO.Path.GetExtension(ftpItem.Name).ToLower();
                if (TextLikeExtensions.Contains(ext))
                {
                    text = true;
                    return true;
                }
                else if ((action & AllowedAction.Edit) != AllowedAction.Edit && ImageExtensions.Contains(ext))
                {
                    return true;
                }
            }
            return false;
        }
        public static string ToSaneString(this DateTime time)
        {
            return time.ToString("dd. MM. yyyy HH:mm:ss");
        }
        public static int CalcPerm(bool r, bool w, bool x) => (r ? 4 : 0) + (w ? 2 : 0) + (x ? 1 : 0);
        public static string StyleMerge(params string[] styles)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var style in styles)
            {
                if (string.IsNullOrWhiteSpace(style)) continue;

                var parts = style.Split(';', StringSplitOptions.RemoveEmptyEntries);

                foreach (var p in parts)
                {
                    var kv = p.Split(':', 2, StringSplitOptions.RemoveEmptyEntries);
                    if (kv.Length == 2)
                    {
                        var key = kv[0].Trim();
                        var value = kv[1].Trim();
                        dict[key] = value; // override duplicates
                    }
                }
            }

            return string.Join("; ", dict.Select(kv => $"{kv.Key}: {kv.Value}"));
        }
        public static string GetMimeType(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 4)
                return "application/octet-stream";

            // PNG
            if (bytes.Length >= 8 &&
                bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
                return "image/png";

            // JPEG
            if (bytes[0] == 0xFF && bytes[1] == 0xD8)
                return "image/jpeg";

            // GIF (GIF87a / GIF89a)
            if (bytes.Length >= 6 &&
                bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46)
                return "image/gif";

            // BMP
            if (bytes[0] == 0x42 && bytes[1] == 0x4D)
                return "image/bmp";

            // WEBP (RIFF....WEBP)
            if (bytes.Length >= 12 &&
                bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 &&
                bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
                return "image/webp";

            // TIFF (little endian & big endian)
            if ((bytes[0] == 0x49 && bytes[1] == 0x49) || (bytes[0] == 0x4D && bytes[1] == 0x4D))
                return "image/tiff";

            // ICO
            if (bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0x01 && bytes[3] == 0x00)
                return "image/x-icon";

            // HEIC / HEIF (ISO Base Media File Format)
            if (bytes.Length >= 12 &&
                bytes[4] == 0x66 && bytes[5] == 0x74 && bytes[6] == 0x79 && bytes[7] == 0x70 &&
                (
                    (bytes[8] == 0x68 && bytes[9] == 0x65 && bytes[10] == 0x69 && bytes[11] == 0x63) || // heic
                    (bytes[8] == 0x68 && bytes[9] == 0x65 && bytes[10] == 0x69 && bytes[11] == 0x66)    // heif
                ))
                return "image/heic";

            // SVG (XML-based, so no fixed binary signature)
            var header = System.Text.Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, 200)).TrimStart();
            if (header.StartsWith("<svg", StringComparison.OrdinalIgnoreCase) ||
                header.Contains("<svg", StringComparison.OrdinalIgnoreCase))
                return "image/svg+xml";

            return "application/octet-stream";
        }
    }
}
