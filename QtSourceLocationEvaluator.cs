using EnvDTE;
using Microsoft.VisualStudio.Shell;
using System;

namespace qml_dbg_tools
{
    public sealed class QtSourceLocationEvaluator : IQtSourceLocationEvaluator
    {
        public StackTraceSourceLocation TryEvaluateSourceLocation(Debugger debugger, string type, string address)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                Expression sourceLocationExpression = debugger.GetExpression($"(({type}*){address})->sourceLocation().sourceFile.d.ptr", true);
                if (sourceLocationExpression == null || !sourceLocationExpression.IsValidValue)
                {
                    return null;
                }

                if (string.IsNullOrWhiteSpace(sourceLocationExpression.Value))
                {
                    return null;
                }

                string[] parts = sourceLocationExpression.Value.Split(' ');

                if (parts.Length >= 2)
                {
                    parts[0] = string.Empty;
                }

                string sourceLocation = string.Join(" ", parts);
                if (sourceLocation.Length < 4)
                {
                    return null;
                }

                sourceLocation = sourceLocation.Substring(3, sourceLocation.Length - 4);

                Expression lineExpression = debugger.GetExpression($"(({type}*){address})->sourceLocation().line");
                Expression columnExpression = debugger.GetExpression($"(({type}*){address})->sourceLocation().column");

                int line = ParseDebuggerInteger(lineExpression);
                int column = ParseDebuggerInteger(columnExpression);

                return new StackTraceSourceLocation(sourceLocation, line, column);
            }
            catch
            {
                return null;
            }
        }

        private static int ParseDebuggerInteger(Expression debuggerExpression)
        {
            if (debuggerExpression == null || !debuggerExpression.IsValidValue)
            {
                return 0;
            }

            return int.TryParse(debuggerExpression.Value, out int parsedValue) ? parsedValue : 0;
        }
    }
}
