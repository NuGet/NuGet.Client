using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Threading;
using NuGet.PackageManagement.UI;
using NuGet.ProjectManagement;

namespace StandaloneUI
{
    internal class StandaloneNuGetUI : INuGetUI
    {
        private INuGetUI _nugetUI;

        public StandaloneNuGetUI(INuGetUI nugetUI)
        {
            _nugetUI = nugetUI;
        }

        public bool PromptForLicenseAcceptance(IEnumerable<PackageLicenseInfo> packages)
        {
            return _nugetUI.PromptForLicenseAcceptance(packages);
        }

        public void LaunchExternalLink(Uri url)
        {
            _nugetUI.LaunchExternalLink(url);
        }

        public void LaunchNuGetOptionsDialog()
        {
            _nugetUI.LaunchNuGetOptionsDialog();
        }

        public bool PromptForPreviewAcceptance(IEnumerable<PreviewResult> actions)
        {
            return _nugetUI.PromptForPreviewAcceptance(actions);
        }

        public void ShowProgressDialog(System.Windows.DependencyObject ownerWindow)
        {
            _nugetUI.ShowProgressDialog(ownerWindow);
        }

        public void CloseProgressDialog()
        {
            _nugetUI.CloseProgressDialog();
        }

        public NuGet.ProjectManagement.INuGetProjectContext ProgressWindow
        {
            get
            {
                return _nugetUI.ProgressWindow;
            }
        }

        public IEnumerable<NuGet.ProjectManagement.NuGetProject> Projects
        {
            get { return _nugetUI.Projects; }
        }

        public bool DisplayPreviewWindow
        {
            get { return _nugetUI.DisplayPreviewWindow; }
        }

        public UserAction UserAction
        {
            get { return _nugetUI.UserAction; }
        }

        public NuGet.PackagingCore.PackageIdentity SelectedPackage
        {
            get { return _nugetUI.SelectedPackage; }
        }

        public void ShowError(string message, string detail)
        {
            _nugetUI.ShowError(message, detail);
        }

        public NuGet.ProjectManagement.FileConflictAction FileConflictAction
        {
            get
            {
                return _nugetUI.FileConflictAction;
            }
        }

        public void RefreshPackageStatus()
        {
            _nugetUI.RefreshPackageStatus();
        }

        public NuGet.Client.SourceRepository ActiveSource
        {
            get
            {
                return _nugetUI.ActiveSource;
            }
        }
    }

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
                    new Action(Start));
                return;
            }

            _textBox.Text = "========== start ============" + Environment.NewLine;
        }

        public void End()
        {
            Log(MessageLevel.Debug, "****** end *********");
        }
    }
}