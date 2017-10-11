// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Windows.Media;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.VisualStudio;

namespace NuGetConsole
{
    /// <summary>
    /// Wrapper console class enabling inline writing into the VS build output window pane.
    /// As the build output is not owned by NuGet but is rather shared with other providers
    /// this implementation doesn't allow certain invasive operations like Clear.
    /// </summary>
    internal sealed class BuildOutputConsole : IOutputConsole
    {
        private const int DefaultConsoleWidth = 120;
        private static Guid BuildWindowPaneGuid = VSConstants.BuildOutput;

        private readonly IVsOutputWindow _vsOutputWindow;
        private IVsOutputWindowPane _outputWindowPane;

        public BuildOutputConsole(IVsOutputWindow vsOutputWindow)
        {
            if (vsOutputWindow == null)
            {
                throw new ArgumentNullException(nameof(vsOutputWindow));
            }

            _vsOutputWindow = vsOutputWindow;
        }

        public int ConsoleWidth => DefaultConsoleWidth;

        [SuppressMessage("Microsoft.VisualStudio.Threading.Analyzers", "VSTHRD010", Justification = "NuGet/Home#4833 Baseline")]
        public void Activate()
        {
            if (_outputWindowPane == null)
            {
                _vsOutputWindow.GetPane(ref BuildWindowPaneGuid, out _outputWindowPane);
            }

            _outputWindowPane?.Activate();
        }

        public void Clear()
        {
        }

        [SuppressMessage("Microsoft.VisualStudio.Threading.Analyzers", "VSTHRD010", Justification = "NuGet/Home#4833 Baseline")]
        public void Write(string text)
        {
            _outputWindowPane?.OutputStringThreadSafe(text);
        }

        public void Write(string text, Color? foreground, Color? background) => Write(text);

        public void WriteBackspace()
        {
            throw new NotSupportedException();
        }

        public void WriteLine(string text) => Write(text + Environment.NewLine);

        public void WriteLine(string format, params object[] args)
        {
            WriteLine(string.Format(CultureInfo.CurrentCulture, format, args));
        }

        public void WriteProgress(string currentOperation, int percentComplete)
        {
        }
    }
}
