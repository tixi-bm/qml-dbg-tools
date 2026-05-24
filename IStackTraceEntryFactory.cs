using EnvDTE;
using EnvDTE90a;

namespace qml_dbg_tools
{
    public interface IStackTraceEntryFactory
    {
        StackTraceEntry Create(StackFrame2 frame, Debugger debugger, StackFrame startFrame);
    }
}
