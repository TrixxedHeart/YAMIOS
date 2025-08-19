using System.Collections.Generic;
using System.IO;

namespace YAMIOS.Backend
{
    public static class PrototypeScanner
    {
        public static List<string> ScanRepo(string repoRoot)
        {
            var files = new List<string>();
            var dirs = new[] { "Resources/Prototypes", "Content/Prototypes", "Prototypes" };
            foreach (var dir in dirs)
            {
                var fullDir = Path.Combine(repoRoot, dir);
                if (Directory.Exists(fullDir))
                {
                    files.AddRange(Directory.GetFiles(fullDir, "*.yml", SearchOption.AllDirectories));
                }
            }
            return files;
        }
    }
}