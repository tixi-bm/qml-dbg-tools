using EnvDTE;
using EnvDTE90a;

namespace qml_dbg_tools
{
    public interface IQtStackFrameContextEvaluator
    {
        string GetQtEvaluationType(StackFrame2 frame);

        void EnsureQtEvaluationContext(Debugger debugger, StackFrame2 targetFrame);
    }
}
