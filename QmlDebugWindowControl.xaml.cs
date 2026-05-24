using EnvDTE;
using EnvDTE80;
using EnvDTE100;
using Microsoft.VisualStudio.Shell;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using EnvDTE90a;
using System.Windows.Input;
using System;

namespace qml_dbg_tools
{
    /// <summary>
    /// Interaction logic for ToolWindow1Control.
    /// </summary>
    public partial class QmlDebugWindowControl : UserControl
    {
        private DTE2 dte;
        private DebuggerEvents debuggerEvents;
        private QmlFileCache qmlFileCache;
        private bool showQtFrames;

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
            this.showQtFramesCheckBox.IsChecked = this.showQtFrames;
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

            this.qmlFileCache = this.qmlFileCache ?? new QmlFileCache(this.dte);

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

        private void showQtFramesCheckBox_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            this.showQtFrames = this.showQtFramesCheckBox.IsChecked == true;
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

            this.qmlFileCache = this.qmlFileCache ?? new QmlFileCache(this.dte);

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
                IStackTraceEntryFactory stackTraceEntryFactory = new StackTraceEntryFactory(
                    new StackTraceSourceResolver(this.qmlFileCache),
                    new QtSourceLocationEvaluator(),
                    new QtStackFrameContextEvaluator());

                foreach (StackFrame2 frame in currentThread.StackFrames)
                {
                    StackTraceEntry stackTraceEntry = stackTraceEntryFactory.Create(frame, debugger, startFrame);
                    if (stackTraceEntry != null && this.ShouldDisplayEntry(stackTraceEntry))
                    {
                        this.stackTraceList.Items.Add(stackTraceEntry);
                    }
                }

                debugger.CurrentStackFrame = startFrame;
            }
            catch (System.Exception ex)
            {
                this.stackTraceList.Items.Add($"Failed to retrieve stack trace: {ex.Message}");
            }
        }


        private bool ShouldDisplayEntry(StackTraceEntry entry)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (entry?.SourceLocation == null || !entry.SourceLocation.IsValid)
            {
                return false;
            }

            string sourcePath = entry.SourceLocation.FilePath;
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return false;
            }

            if (!this.showQtFrames && IsQtMetaCallEntry(entry.FunctionName))
            {
                return false;
            }

            if (IsQmlEntry(entry, sourcePath))
            {
                return true;
            }

            if (IsQtModule(entry.Module))
            {
                return this.showQtFrames;
            }

            return entry.UserCode && File.Exists(sourcePath);
        }

        private static bool IsQtModule(string moduleName)
        {
            return !string.IsNullOrWhiteSpace(moduleName)
                && moduleName.StartsWith("Qt6", StringComparison.OrdinalIgnoreCase)
                && moduleName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsQmlEntry(StackTraceEntry entry, string sourcePath)
        {
            return sourcePath.EndsWith(".qml", StringComparison.OrdinalIgnoreCase)
                || sourcePath.StartsWith("qrc:/", StringComparison.OrdinalIgnoreCase)
                || string.Equals(entry.Language, "QML", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsQtMetaCallEntry(string functionName)
        {
            return !string.IsNullOrWhiteSpace(functionName)
                && (functionName.EndsWith("::qt_metacall", StringComparison.OrdinalIgnoreCase)
                    || functionName.EndsWith("::qt_static_metacall", StringComparison.OrdinalIgnoreCase));
        }

        private bool IsPathInCurrentSolution(string path)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return false;
            }

            string solutionPath = this.dte?.Solution?.FullName;
            if (string.IsNullOrWhiteSpace(solutionPath))
            {
                return true;
            }

            string solutionRoot = Path.GetDirectoryName(solutionPath);
            if (string.IsNullOrWhiteSpace(solutionRoot))
            {
                return true;
            }

            string fullPath = Path.GetFullPath(path);
            string fullSolutionRoot = Path.GetFullPath(solutionRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(fullSolutionRoot, StringComparison.OrdinalIgnoreCase);
        }

        private void stackTraceList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            ListBoxItem container = ItemsControl.ContainerFromElement(this.stackTraceList, e.OriginalSource as DependencyObject) as ListBoxItem;
            if (container?.DataContext is StackTraceEntry entry)
            {
                this.NavigateToStackTraceEntry(entry);
                e.Handled = true;
            }
        }

        private void NavigateToStackTraceEntry(StackTraceEntry entry)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (entry?.SourceLocation == null || !entry.SourceLocation.IsValid)
            {
                return;
            }

            if (this.dte == null)
            {
                this.dte = Package.GetGlobalService(typeof(DTE)) as DTE2;
            }

            if (this.dte == null)
            {
                return;
            }

            try
            {
                EnvDTE.Window window = this.dte.ItemOperations.OpenFile(entry.SourceLocation.FilePath);
                if (window?.Document?.Object("TextDocument") is TextDocument textDocument)
                {
                    textDocument.Selection.MoveToLineAndOffset(entry.SourceLocation.Line, entry.SourceLocation.Column > 0 ? entry.SourceLocation.Column : 1, false);
                }

                window?.Activate();
            }
            catch
            {
            }
        }

    }

    public sealed class StackTraceEntry
    {
        public StackTraceEntry(string module, string functionName, string language, StackTraceSourceLocation sourceLocation, bool userCode)
        {
            this.Module = module ?? string.Empty;
            this.FunctionName = functionName ?? string.Empty;
            this.Language = language ?? string.Empty;
            this.SourceLocation = sourceLocation;
            this.UserCode = userCode;
        }

        public string Module { get; }

        public string FunctionName { get; }

        public string Language { get; }

        public StackTraceSourceLocation SourceLocation { get; }

        public bool UserCode { get; }

        public override string ToString()
        {
            return $"{this.Module} {this.FunctionName} ({this.Language})";
        }
    }

    public sealed class StackTraceSourceLocation
    {
        public StackTraceSourceLocation(string filePath, int line, int column)
        {
            this.FilePath = filePath ?? string.Empty;
            this.Line = line;
            this.Column = column;
        }

        public string FilePath { get; }

        public int Line { get; }

        public int Column { get; }

        public bool IsValid => !string.IsNullOrWhiteSpace(this.FilePath) && this.Line > 0;

        public override string ToString()
        {
            return this.Column > 0 ? $"{this.FilePath}:{this.Line}:{this.Column}" : $"{this.FilePath}:{this.Line}";
        }
    }
}