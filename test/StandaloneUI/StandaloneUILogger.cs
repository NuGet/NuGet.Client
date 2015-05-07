// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Windows.Controls;
using System.Windows.Threading;
using NuGet.PackageManagement.UI;
using NuGet.ProjectManagement;

namespace StandaloneUI
{
    internal class StandaloneUILogger : INuGetUILogger
    {
        private readonly TextBox _textBox;
        private readonly Dispatcher _uiDispatcher;
        private readonly ScrollViewer _scrollViewer;

        public StandaloneUILogger(TextBox textBox, ScrollViewer scrollViewer)
        {
            _textBox = textBox;
            _scrollViewer = scrollViewer;
            _uiDispatcher = Dispatcher.CurrentDispatcher;
        }

        public void Log(MessageLevel level, string message, params object[] args)
        {
            if (!_uiDispatcher.CheckAccess())
            {
                _uiDispatcher.Invoke(
                    new Action<MessageLevel, string, object[]>(Log),
                    level,
                    message,
                    args);
                return;
            }

            var line = string.Format(message, args) + Environment.NewLine;
            _textBox.Text += line;
            _scrollViewer.ScrollToEnd();
        }

        public void Start()
        {
            if (!_uiDispatcher.CheckAccess())
            {
                _uiDispatcher.Invoke(
                    Start);
                return;
            }

            _textBox.Text = "========== start ============" + Environment.NewLine;
        }

        public void End()
        {
            Log(MessageLevel.Debug, "****** end *********");
        }

        public void ReportError(string message)
        {
            Log(MessageLevel.Debug, "Report error: {0}", message);
        }
    }
}
