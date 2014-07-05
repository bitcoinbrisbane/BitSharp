using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Esent.Test
{
    public static class EsentTests
    {
        public static string GetBaseDirectory()
        {
            return Path.Combine(Path.GetTempPath(), "BitSharp", "Tests");
        }

        public static string PrepareBaseDirectory()
        {
            CleanBaseDirectory();

            var path = GetBaseDirectory();
            Directory.CreateDirectory(path);

            return path;
        }

        public static void CleanBaseDirectory()
        {
            var path = GetBaseDirectory();

            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
    }
}
