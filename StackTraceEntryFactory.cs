using EnvDTE;
using EnvDTE90a;
using Microsoft.VisualStudio.Shell;
using System;
using System.IO;

namespace qml_dbg_tools
{
    public sealed class StackTraceEntryFactory : IStackTraceEntryFactory
    {
        private readonly IStackTraceSourceResolver sourceResolver;
        private readonly IQtSourceLocationEvaluator qtSourceLocationEvaluator;
        private readonly IQtStackFrameContextEvaluator qtStackFrameContextEvaluator;

        public StackTraceEntryFactory(
            IStackTraceSourceResolver sourceResolver,
            IQtSourceLocationEvaluator qtSourceLocationEvaluator,
            IQtStackFrameContextEvaluator qtStackFrameContextEvaluator)
        {
            this.sourceResolver = sourceResolver;
            this.qtSourceLocationEvaluator = qtSourceLocationEvaluator;
            this.qtStackFrameContextEvaluator = qtStackFrameContextEvaluator;
        }

        public StackTraceEntry Create(StackFrame2 frame, Debugger debugger, StackFrame startFrame)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (frame == null)
            {
                return null;
            }

            string moduleName = string.IsNullOrWhiteSpace(frame.Module) ? string.Empty : Path.GetFileName(frame.Module);
            string functionName = string.IsNullOrWhiteSpace(frame.FunctionName) ? "<unknown function>" : frame.FunctionName;
            string displayLanguage = string.IsNullOrWhiteSpace(frame.Language) ? string.Empty : frame.Language;
            bool userCode = frame.UserCode;
            StackTraceSourceLocation sourceLocation = null;

            if (!string.IsNullOrWhiteSpace(frame.FileName) && frame.LineNumber > 0)
            {
                sourceLocation = new StackTraceSourceLocation(frame.FileName, (int)frame.LineNumber, 1);
            }

            sourceLocation = this.sourceResolver.ResolveSourceLocation(sourceLocation, expectQml: false);
            string qtEvaluationType = this.qtStackFrameContextEvaluator.GetQtEvaluationType(frame);

            if (string.IsNullOrWhiteSpace(qtEvaluationType))
            {
                return new StackTraceEntry(moduleName, functionName, displayLanguage, sourceLocation, userCode);
            }

            this.qtStackFrameContextEvaluator.EnsureQtEvaluationContext(debugger, frame);

            foreach (Expression localValue in frame.Locals)
            {
                if (!string.Equals(localValue.Name, "this", StringComparison.Ordinal))
                {
                    continue;
                }

                string[] localParts = (localValue.Value ?? string.Empty).Split(' ');
                if (localParts.Length == 0 || string.IsNullOrWhiteSpace(localParts[0]))
                {
                    continue;
                }

                StackTraceSourceLocation qmlSourceLocation = this.qtSourceLocationEvaluator.TryEvaluateSourceLocation(debugger, qtEvaluationType, localParts[0]);
                if (qmlSourceLocation == null)
                {
                    continue;
                }

                sourceLocation = this.sourceResolver.ResolveSourceLocation(qmlSourceLocation, expectQml: true);
                string qmlFunctionName = this.sourceResolver.TryReadSourceLine(sourceLocation);
                if (!string.IsNullOrWhiteSpace(qmlFunctionName))
                {
                    functionName = qmlFunctionName;
                }

                if (sourceLocation != null && !string.IsNullOrWhiteSpace(sourceLocation.FilePath))
                {
                    string qmlFileName = Path.GetFileName(sourceLocation.FilePath);
                    if (!string.IsNullOrWhiteSpace(qmlFileName))
                    {
                        moduleName = qmlFileName;
                    }
                }

                displayLanguage = "QML";
                userCode = true;
            }

            return new StackTraceEntry(moduleName, functionName, displayLanguage, sourceLocation, userCode);
        }
    }
}
