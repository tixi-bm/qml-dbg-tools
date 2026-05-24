using EnvDTE;
using EnvDTE90a;
using Microsoft.VisualStudio.Shell;
using System;
using System.IO;

namespace qml_dbg_tools
{
    public sealed class QtStackFrameContextEvaluator : IQtStackFrameContextEvaluator
    {
        private static readonly string[] QtEvaluationFunctions =
        {
            "QQmlBinding::update",
            "QQmlBoundSignalExpression::evaluate",
        };

        private bool qtContextEstablished;

        public string GetQtEvaluationType(StackFrame2 frame)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (frame == null || !IsQtQmlModule(frame.Module))
            {
                return null;
            }

            string functionName = frame.FunctionName;
            if (string.IsNullOrWhiteSpace(functionName))
            {
                return null;
            }

            foreach (string qtFunction in QtEvaluationFunctions)
            {
                if (!string.Equals(functionName, qtFunction, StringComparison.Ordinal))
                {
                    continue;
                }

                int separatorIndex = qtFunction.IndexOf("::", StringComparison.Ordinal);
                return separatorIndex > 0 ? qtFunction.Substring(0, separatorIndex) : null;
            }

            return null;
        }

        public void EnsureQtEvaluationContext(Debugger debugger, StackFrame2 targetFrame)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (debugger == null || targetFrame == null)
            {
                return;
            }

            if (this.qtContextEstablished)
            {
                return;
            }

            if (!this.RequiresQtContextSwitch(targetFrame, debugger.CurrentStackFrame))
            {
                this.qtContextEstablished = true;
                return;
            }

            debugger.CurrentStackFrame = targetFrame;
            this.qtContextEstablished = true;
        }

        private bool RequiresQtContextSwitch(StackFrame2 targetFrame, StackFrame currentFrame)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (targetFrame == null || !IsQtQmlModule(targetFrame.Module))
            {
                return false;
            }

            return !IsQtQmlModule(currentFrame?.Module);
        }

        private static bool IsQtQmlModule(string module)
        {
            string moduleName = string.IsNullOrWhiteSpace(module) ? string.Empty : Path.GetFileName(module);
            return string.Equals(moduleName, "Qt6Qml.dll", StringComparison.OrdinalIgnoreCase)
                || string.Equals(moduleName, "Qt6Qmld.dll", StringComparison.OrdinalIgnoreCase);
        }
    }
}
