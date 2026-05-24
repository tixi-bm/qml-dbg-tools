using EnvDTE;

namespace qml_dbg_tools
{
    public interface IQtSourceLocationEvaluator
    {
        StackTraceSourceLocation TryEvaluateSourceLocation(Debugger debugger, string type, string address);
    }
}
