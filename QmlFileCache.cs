using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace qml_dbg_tools
{
    public sealed class QmlFileCache
    {
        private readonly DTE2 dte;
        private readonly object cacheLock = new object();
        private readonly TimeSpan refreshInterval = TimeSpan.FromSeconds(10);
        private readonly Dictionary<string, List<QmlFileEntry>> qmlFilesByName = new Dictionary<string, List<QmlFileEntry>>(StringComparer.OrdinalIgnoreCase);
        private DateTime lastRefreshUtc = DateTime.MinValue;

        public QmlFileCache(DTE2 dte)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            this.dte = dte;
        }

        public string TryResolvePath(string candidatePath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (string.IsNullOrWhiteSpace(candidatePath))
            {
                return null;
            }

            if (!IsQrcPath(candidatePath))
            {
                if (File.Exists(candidatePath))
                {
                    return Path.GetFullPath(candidatePath);
                }

                return null;
            }

            this.EnsureCache();

            string normalized = NormalizeQrcPath(candidatePath);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            string[] inputSegments = SplitSegments(normalized);
            if (inputSegments.Length == 0)
            {
                return null;
            }

            string fileName = inputSegments[inputSegments.Length - 1];
            if (!this.qmlFilesByName.TryGetValue(fileName, out List<QmlFileEntry> candidates))
            {
                return null;
            }

            QmlFileEntry best = null;
            int bestScore = -1;

            foreach (QmlFileEntry candidate in candidates)
            {
                int score = ScoreByMatchingSuffixSegments(inputSegments, candidate.Segments);
                if (score <= 0)
                {
                    continue;
                }

                if (score > bestScore)
                {
                    best = candidate;
                    bestScore = score;
                    continue;
                }

                if (score == bestScore && best != null && candidate.FullPath.Length < best.FullPath.Length)
                {
                    best = candidate;
                }
            }

            return best?.FullPath;
        }

        private static bool IsQrcPath(string path)
        {
            return path.StartsWith("qrc:/", StringComparison.OrdinalIgnoreCase);
        }

        private void EnsureCache()
        {
            lock (this.cacheLock)
            {
                if ((DateTime.UtcNow - this.lastRefreshUtc) < this.refreshInterval && this.qmlFilesByName.Count > 0)
                {
                    return;
                }

                this.qmlFilesByName.Clear();

                string solutionRoot = GetSolutionRoot(this.dte);
                if (!string.IsNullOrWhiteSpace(solutionRoot) && Directory.Exists(solutionRoot))
                {
                    IEnumerable<string> qmlFiles = EnumerateQmlFiles(solutionRoot);
                    foreach (string qmlFile in qmlFiles)
                    {
                        QmlFileEntry entry = CreateEntry(solutionRoot, qmlFile);
                        string fileName = Path.GetFileName(qmlFile);

                        if (!this.qmlFilesByName.TryGetValue(fileName, out List<QmlFileEntry> list))
                        {
                            list = new List<QmlFileEntry>();
                            this.qmlFilesByName[fileName] = list;
                        }

                        list.Add(entry);
                    }
                }

                this.lastRefreshUtc = DateTime.UtcNow;
            }
        }

        private static string[] SplitSegments(string path)
        {
            return path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static string NormalizeQrcPath(string qrcPath)
        {
            if (Uri.TryCreate(qrcPath, UriKind.Absolute, out Uri uri) && uri.Scheme.Equals("qrc", StringComparison.OrdinalIgnoreCase))
            {
                return uri.AbsolutePath.Trim('/');
            }

            string raw = qrcPath.Substring("qrc:/".Length);
            int slashIndex = raw.IndexOf('/');
            if (slashIndex >= 0 && slashIndex < raw.Length - 1)
            {
                return raw.Substring(slashIndex + 1);
            }

            return string.Empty;
        }

        private static int ScoreByMatchingSuffixSegments(string[] inputSegments, string[] candidateSegments)
        {
            int score = 0;
            int i = inputSegments.Length - 1;
            int j = candidateSegments.Length - 1;

            while (i >= 0 && j >= 0)
            {
                if (!inputSegments[i].Equals(candidateSegments[j], StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                score++;
                i--;
                j--;
            }

            return score;
        }

        private static QmlFileEntry CreateEntry(string solutionRoot, string fullPath)
        {
            string relativePath = fullPath;
            if (fullPath.StartsWith(solutionRoot, StringComparison.OrdinalIgnoreCase))
            {
                relativePath = fullPath.Substring(solutionRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }

            string[] segments = SplitSegments(relativePath);
            return new QmlFileEntry(Path.GetFullPath(fullPath), segments);
        }

        private static IEnumerable<string> EnumerateQmlFiles(string rootDirectory)
        {
            Stack<string> directories = new Stack<string>();
            directories.Push(rootDirectory);

            while (directories.Count > 0)
            {
                string currentDirectory = directories.Pop();

                if (File.Exists(Path.Combine(currentDirectory, "CMakeCache.txt")))
                {
                    continue;
                }

                IEnumerable<string> qmlFiles;
                IEnumerable<string> childDirectories;

                try
                {
                    qmlFiles = Directory.EnumerateFiles(currentDirectory, "*.qml", SearchOption.TopDirectoryOnly);
                    childDirectories = Directory.EnumerateDirectories(currentDirectory, "*", SearchOption.TopDirectoryOnly);
                }
                catch
                {
                    continue;
                }

                foreach (string qmlFile in qmlFiles)
                {
                    yield return qmlFile;
                }

                foreach (string childDirectory in childDirectories)
                {
                    directories.Push(childDirectory);
                }
            }
        }

        private static string GetSolutionRoot(DTE2 dte)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (dte?.Solution == null || string.IsNullOrWhiteSpace(dte.Solution.FullName))
            {
                return null;
            }

            Console.WriteLine(Directory.GetCurrentDirectory());

            return dte.Solution.FullName;
        }

        private sealed class QmlFileEntry
        {
            public QmlFileEntry(string fullPath, string[] segments)
            {
                this.FullPath = fullPath;
                this.Segments = segments ?? Array.Empty<string>();
            }

            public string FullPath { get; }

            public string[] Segments { get; }
        }
    }
}
