using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReindexAutomation.Client.Utils
{
    public static class FileHelper
    {
        public static string MinimizePath(string path, int maxLength)
        {
            var pathes = path.Split('\\');
            var finalPath = string.Empty;
            for (var i = pathes.Length - 1; i >= 1; i--)
            {
                finalPath = "\\" + pathes[i] + finalPath;
                if (finalPath.Length > maxLength)
                {
                    finalPath = ".." + finalPath;
                    break;
                }
            }
            if (finalPath.Length <= maxLength)
            {
                finalPath = pathes[0] + finalPath;
            }
            return finalPath;
        }
    }
}
