using EnvDTE;
using EnvDTE80;
using EnvDTE100;
using Microsoft.VisualStudio.Shell;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using EnvDTE90a;
using System.Text.RegularExpressions;
using System.Linq;

namespace qml_dbg_tools
{
    /// <summary>
    /// Interaction logic for ToolWindow1Control.
    /// </summary>
    public partial class QmlDebugWindowControl : UserControl
    {
        private DTE2 dte;
        private DebuggerEvents debuggerEvents;

        /// <summary>
        /// Initializes a new instance of the <see cref="QmlDebugWindowControl"/> class.
        /// </summary>
        public QmlDebugWindowControl()
        {
            this.InitializeComponent();
            this.Loaded += this.QmlDebugWindowControl_Loaded;
            this.Unloaded += this.QmlDebugWindowControl_Unloaded;
        }

        private void QmlDebugWindowControl_Loaded(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            this.InitializeDebuggerEvents();
            this.RefreshStackTrace();
        }

        private void QmlDebugWindowControl_Unloaded(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            this.UnsubscribeDebuggerEvents();
        }

        private void InitializeDebuggerEvents()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (this.debuggerEvents != null)
            {
                return;
            }

            this.dte = Package.GetGlobalService(typeof(DTE)) as DTE2;
            if (this.dte == null)
            {
                return;
            }

            this.debuggerEvents = this.dte.Events.DebuggerEvents;
            this.debuggerEvents.OnEnterBreakMode += this.DebuggerEvents_OnEnterBreakMode;
            this.debuggerEvents.OnContextChanged += this.DebuggerEvents_OnContextChanged;
            this.debuggerEvents.OnEnterRunMode += this.DebuggerEvents_OnEnterRunMode;
            this.debuggerEvents.OnEnterDesignMode += this.DebuggerEvents_OnEnterDesignMode;
        }

        private void UnsubscribeDebuggerEvents()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (this.debuggerEvents == null)
            {
                return;
            }

            this.debuggerEvents.OnEnterBreakMode -= this.DebuggerEvents_OnEnterBreakMode;
            this.debuggerEvents.OnContextChanged -= this.DebuggerEvents_OnContextChanged;
            this.debuggerEvents.OnEnterRunMode -= this.DebuggerEvents_OnEnterRunMode;
            this.debuggerEvents.OnEnterDesignMode -= this.DebuggerEvents_OnEnterDesignMode;
            this.debuggerEvents = null;
        }

        private void DebuggerEvents_OnEnterBreakMode(dbgEventReason reason, ref dbgExecutionAction executionAction)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            this.RefreshStackTrace();
        }

        private void DebuggerEvents_OnContextChanged(EnvDTE.Process newProcess, EnvDTE.Program newProgram, Thread newThread, StackFrame newStackFrame)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            this.RefreshStackTrace();
        }

        private void DebuggerEvents_OnEnterRunMode(dbgEventReason reason)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            this.stackTraceList.Items.Clear();
            this.stackTraceList.Items.Add("Debugger is running.");
        }

        private void DebuggerEvents_OnEnterDesignMode(dbgEventReason reason)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            this.stackTraceList.Items.Clear();
            this.stackTraceList.Items.Add("Debugger is not active.");
        }

        /// <summary>
        /// Handles click on the button by querying and displaying current stack frames.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event args.</param>
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:ElementMustBeginWithUpperCaseLetter", Justification = "Default event handler naming pattern")]
        private void refreshButton_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            this.RefreshStackTrace();
        }

        private void RefreshStackTrace()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            this.stackTraceList.Items.Clear();

            if (this.dte == null)
            {
                this.dte = Package.GetGlobalService(typeof(DTE)) as DTE2;
            }

            if (this.dte?.Debugger == null)
            {
                this.stackTraceList.Items.Add("Debugger service unavailable.");
                return;
            }

            Debugger debugger = this.dte.Debugger;
            if (debugger.CurrentMode != dbgDebugMode.dbgBreakMode)
            {
                this.stackTraceList.Items.Add("Break into your process first (Debug mode must be Break).");
                return;
            }

            try
            {
                Thread currentThread = debugger.CurrentThread;
                if (currentThread == null || currentThread.StackFrames == null || currentThread.StackFrames.Count == 0)
                {
                    this.stackTraceList.Items.Add("No stack frames available for the current thread.");
                    return;
                }

                StackFrame startFrame = debugger.CurrentStackFrame;

                foreach (StackFrame2 frame in currentThread.StackFrames)
                {
                    string functionName = string.IsNullOrWhiteSpace(frame.FunctionName) ? "<unknown function>" : frame.FunctionName;
                    string fileName = string.IsNullOrWhiteSpace(frame.FileName) ? string.Empty : Path.GetFileName(frame.FileName);
                    string sourceLocation = null;

                    if (!string.IsNullOrWhiteSpace(frame.Module))
                    {
                        string moduleName = Path.GetFileName(frame.Module);

                        if (moduleName == "Qt6Qml.dll" ||  moduleName == "Qt6Qmld.dll")
                        {
                            string type = "";

                            if (frame.FunctionName == "QQmlBinding::update")
                            {
                                type = "QQmlBinding";
                            }

                            if (frame.FunctionName == "QQmlBoundSignalExpression::evaluate")
                            {
                                type = "QQmlBoundSignalExpression";
                            }

                            if (type != "")
                            {
                                // Change into a frame of Qml.dll, so that the types are present
                                if (startFrame == debugger.CurrentStackFrame)
                                {
                                    debugger.CurrentStackFrame = frame;
                                }

                                foreach (EnvDTE.Expression x in frame.Locals)
                                {
                                    if (x.Name == "this")
                                    {
                                        sourceLocation = TryEvaluateSourceLocation(debugger, type, x.Value.Split(' ')[0]);
                                    }
                                }
                            }
                        }
                    }

                    string location = string.IsNullOrWhiteSpace(fileName)
                        ? "No source location"
                        : (frame.LineNumber > 0 ? $"{fileName}:{frame.LineNumber}" : fileName);

                    if (!string.IsNullOrWhiteSpace(sourceLocation))
                    {
                        location += $" | QML source: {sourceLocation}";
                    }

                    this.stackTraceList.Items.Add($"{frame.Language}: {frame.Module} {functionName} ({location})");
                }

                debugger.CurrentStackFrame = startFrame;
            }
            catch (System.Exception ex)
            {
                this.stackTraceList.Items.Add($"Failed to retrieve stack trace: {ex.Message}");
            }
        }

        private static string TryEvaluateSourceLocation(Debugger debugger, string type, string address)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                EnvDTE.Expression sourceLocationExpression = debugger.GetExpression($"(({type}*){address})->sourceLocation().sourceFile.d.ptr", true);
                if (sourceLocationExpression == null || !sourceLocationExpression.IsValidValue)
                {
                    return null;
                }

                if (string.IsNullOrWhiteSpace(sourceLocationExpression.Value))
                {
                    return "";
                }

                string[] parts = sourceLocationExpression.Value.Split(' ');

                if (parts.Length >= 2)
                {
                    parts[0] = "";
                }

                string sourceLocation = string.Join(" ", parts);
                sourceLocation = sourceLocation.Substring(2, sourceLocation.Length - 3);

                EnvDTE.Expression lineExpression = debugger.GetExpression($"(({type}*){address})->sourceLocation().line");
                EnvDTE.Expression columnExpression = debugger.GetExpression($"(({type}*){address})->sourceLocation().column");

                string line = (lineExpression == null || !lineExpression.IsValidValue) ? "" : lineExpression.Value;
                string column = (columnExpression == null || !columnExpression.IsValidValue) ? "" : columnExpression.Value;

                return $"{sourceLocation}:{line}:{column}";
            }
            catch
            {
                return null;
            }
        }
    }
}