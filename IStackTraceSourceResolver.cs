using Microsoft.VisualStudio.Shell;

namespace qml_dbg_tools
{
    public interface IStackTraceSourceResolver
    {
        StackTraceSourceLocation ResolveSourceLocation(StackTraceSourceLocation sourceLocation, bool expectQml);

        string TryReadSourceLine(StackTraceSourceLocation sourceLocation);
    }
}
