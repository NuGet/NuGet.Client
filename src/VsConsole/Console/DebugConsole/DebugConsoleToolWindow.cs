// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using NuGet;
using NuGet.PackageManagement.VisualStudio;
using NuGetConsole.DebugConsole;

namespace NuGetConsole.Implementation
{
    [Guid("c01ab4c3-68be-4add-b51d-26fa1d26245f")]
    public sealed class DebugConsoleToolWindow : ToolWindowPane
    {
        private IWpfConsoleService _consoleService;
        private DebugWindow _debugWindow;
        private IWpfConsole _console;
        private bool _active;
        private IEnumerable<IDebugConsoleController> _sources;
        private EventHandler<DebugConsoleMessageEventArgs> _handler;
        private DebugConsoleViewModel _viewModel;

        public const string ContentType = "PackageManagerDebugConsole";

        [SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters", MessageId = "Microsoft.VisualStudio.Shell.ToolWindowPane.set_Caption(System.String)")]
        public DebugConsoleToolWindow()
            : base(null)
        {
            // TODO: put this in the resources
            this.Caption = "Package Manager Debug Console";
            this.BitmapResourceID = 301;
            this.BitmapIndex = 0;

            _active = false;
            _viewModel = new DebugConsoleViewModel();

            _handler = new EventHandler<DebugConsoleMessageEventArgs>(HandleMessage);
            _debugWindow = new DebugWindow { DataContext = _viewModel };
            this.Content = _debugWindow;
        }

        public override void OnToolWindowCreated()
        {
            var consoleService = ServiceLocator.GetInstance<IWpfConsoleService>();

            CreateConsole(consoleService);

            _active = true;

            base.OnToolWindowCreated();
        }

        private IEnumerable<IDebugConsoleController> MessageSources
        {
            get
            {
                if (_sources == null)
                {
                    var source = ServiceLocator.GetInstance<IDebugConsoleController>();

                    List<IDebugConsoleController> sources = new List<IDebugConsoleController>();

                    if (source != null)
                    {
                        sources.Add(source);
                    }

                    _sources = sources;
                }

                return _sources;
            }
        }

        public void CreateConsole(IWpfConsoleService consoleService)
        {
            _consoleService = consoleService;

            _console = _consoleService.CreateConsole(ServiceLocator.PackageServiceProvider, ContentType, "nugetdebug");

            _console.StartWritingOutput();

            IVsTextView view = _console.VsTextView as IVsTextView;

            var adapterFactory = ServiceLocator.GetInstance<IVsEditorAdaptersFactoryService>();

            IWpfTextView wpfView = adapterFactory.GetWpfTextView(view);

            // adjust the view options
            if (wpfView != null)
            {
                wpfView.Options.SetOptionValue(DefaultTextViewOptions.WordWrapStyleId, WordWrapStyles.None);

                Brush blackBg = Brushes.Black;
                blackBg.Freeze();

                wpfView.Background = blackBg;
            }

            UIElement element = _console.Content as UIElement;
            _debugWindow.DebugGrid.Children.Add(element);

            // Add message sources
            AttachEvents();
        }

        [SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters", MessageId = "NuGetConsole.IConsole.Write(System.String,System.Nullable<System.Windows.Media.Color>,System.Nullable<System.Windows.Media.Color>)", Justification = "String does not require localization.")]
        public void Log(DateTime timestamp, string message, TraceEventType level, string source)
        {
            if (IsActive)
            {
                // Map the type
                var mapped = MapType(level);

                // Check if we should write the message
                if (mapped >= _viewModel.ActiveLevel)
                {
                    _console.Write(
                        String.Format(CultureInfo.CurrentCulture, "[{0:O}][{1}]{2}", timestamp, Shorten(source), message) + Environment.NewLine,
                        ConvertColor(mapped), null);
                }

                // TODO: Allow source filtering?
            }
        }

        // Shortens NuGet. trace source names.
        private static string Shorten(string source)
        {
            if (source.StartsWith("NuGet.", StringComparison.OrdinalIgnoreCase))
            {
                return source.Split('.').Last();
            }
            return source;
        }

        private static DebugConsoleLevel MapType(TraceEventType level)
        {
            switch (level)
            {
                case TraceEventType.Critical:
                    return DebugConsoleLevel.Critical;
                case TraceEventType.Error:
                    return DebugConsoleLevel.Error;
                case TraceEventType.Information:
                    return DebugConsoleLevel.Info;
                case TraceEventType.Warning:
                    return DebugConsoleLevel.Warning;
                default:
                    return DebugConsoleLevel.Trace;
            }
        }

        private static Color ConvertColor(DebugConsoleLevel level)
        {
            switch (level)
            {
                case DebugConsoleLevel.Critical:
                    return Colors.Red;
                case DebugConsoleLevel.Error:
                    return Colors.Red;
                case DebugConsoleLevel.Warning:
                    return Colors.Yellow;
                case DebugConsoleLevel.Info:
                    return Colors.Green;
                case DebugConsoleLevel.Trace:
                    return Colors.Silver;
                default:
                    return Colors.White;
            }
        }

        public bool IsActive
        {
            get { return _active; }
        }

        private void AttachEvents()
        {
            foreach (var source in MessageSources)
            {
                source.OnMessage += _handler;
            }
        }

        private void DetachEvents()
        {
            foreach (var source in MessageSources)
            {
                source.OnMessage -= _handler;
            }
        }

        private void HandleMessage(object sender, DebugConsoleMessageEventArgs args)
        {
            Log(args.Timestamp, args.Message, args.Level, args.Source);
        }

        protected override void Dispose(bool disposing)
        {
            DetachEvents();

            base.Dispose(disposing);
        }
    }
}
