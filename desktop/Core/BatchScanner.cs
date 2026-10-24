using System;
using System.Collections.Generic;
using System.IO;

namespace FolderSnapDesktop.Core
{
    public class BatchScanner
    {
        private readonly HashSet<string> _blacklistedFolderNames;

        public BatchScanner(IEnumerable<string> blacklist = null)
        {
            _blacklistedFolderNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 默认常见忽略目录与截图输出目录
            string[] defaultBlacklist = new string[]
            {
                ".git", ".svn", ".hg", ".vscode", ".idea",
                "node_modules", "bin", "obj", "dist", "build",
                "__pycache__", ".vs", "_FolderSnap_Screenshots"
            };

            foreach (string name in defaultBlacklist)
            {
                _blacklistedFolderNames.Add(name);
            }

            if (blacklist != null)
            {
                foreach (string name in blacklist)
                {
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        _blacklistedFolderNames.Add(name.Trim());
                    }
                }
            }
        }

        public List<string> ScanDirectories(string rootPath, bool includeRoot = true, string excludePath = null, int maxDepth = -1)
        {
            List<string> result = new List<string>();
            if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath)) return result;

            if (includeRoot)
            {
                result.Add(rootPath);
            }

            TraverseRecursive(rootPath, result, excludePath, 1, maxDepth);
            return result;
        }

        private void TraverseRecursive(string currentDir, List<string> result, string excludePath, int currentDepth, int maxDepth)
        {
            if (maxDepth >= 0 && currentDepth > maxDepth)
            {
                return;
            }

            try
            {
                string[] subDirs = Directory.GetDirectories(currentDir);
                foreach (string subDir in subDirs)
                {
                    string dirName = Path.GetFileName(subDir);
                    if (_blacklistedFolderNames.Contains(dirName))
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(excludePath) &&
                        string.Equals(subDir.TrimEnd('\\', '/'), excludePath.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    result.Add(subDir);
                    TraverseRecursive(subDir, result, excludePath, currentDepth + 1, maxDepth);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("访问目录跳过 [{0}]: {1}", currentDir, ex.Message);
            }
        }


        public static string GenerateSafeFileName(string rootPath, string folderPath)
        {
            if (string.Equals(rootPath.TrimEnd('\\', '/'), folderPath.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
            {
                return "00_ROOT_" + Path.GetFileName(rootPath) + ".png";
            }

            string relative = folderPath.Substring(rootPath.Length).TrimStart('\\', '/');
            string safeName = relative.Replace('\\', '_').Replace('/', '_');

            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
            {
                safeName = safeName.Replace(c, '_');
            }

            return safeName + ".png";
        }
    }
}
