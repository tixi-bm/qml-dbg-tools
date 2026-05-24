using Microsoft.VisualStudio.Shell;
using System;
using System.IO;

namespace qml_dbg_tools
{
    public sealed class StackTraceSourceResolver : IStackTraceSourceResolver
    {
        private readonly QmlFileCache qmlFileCache;

        public StackTraceSourceResolver(QmlFileCache qmlFileCache)
        {
            this.qmlFileCache = qmlFileCache;
        }

        public StackTraceSourceLocation ResolveSourceLocation(StackTraceSourceLocation sourceLocation, bool expectQml)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (sourceLocation == null || string.IsNullOrWhiteSpace(sourceLocation.FilePath))
            {
                return sourceLocation;
            }

            bool isQrcPath = sourceLocation.FilePath.StartsWith("qrc:/", StringComparison.OrdinalIgnoreCase);
            bool isQmlLikePath = isQrcPath || sourceLocation.FilePath.EndsWith(".qml", StringComparison.OrdinalIgnoreCase);

            if (!isQmlLikePath && !expectQml)
            {
                return sourceLocation;
            }

            string resolvedPath = this.qmlFileCache?.TryResolvePath(sourceLocation.FilePath);
            if (string.IsNullOrWhiteSpace(resolvedPath))
            {
                return sourceLocation;
            }

            return new StackTraceSourceLocation(resolvedPath, sourceLocation.Line, sourceLocation.Column);
        }

        public string TryReadSourceLine(StackTraceSourceLocation sourceLocation)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (sourceLocation == null || !sourceLocation.IsValid || !File.Exists(sourceLocation.FilePath))
            {
                return string.Empty;
            }

            try
            {
                string[] lines = File.ReadAllLines(sourceLocation.FilePath);
                int lineIndex = sourceLocation.Line - 1;

                if (lineIndex < 0 || lineIndex >= lines.Length)
                {
                    return string.Empty;
                }

                return lines[lineIndex].Trim();
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
