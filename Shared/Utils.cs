using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    }
}
