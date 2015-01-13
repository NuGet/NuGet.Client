using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using Resx = NuGet.PackageManagement.UI;
using NuGet.ProjectManagement;
using NuGet.PackagingCore;
using NuGet.Client.VisualStudio;

namespace NuGet.PackageManagement.UI
{
    // The DataContext of this control is DetailControlModel, i.e. either 
    // PackageSolutionDetailControlModel or PackageDetailControlModel.
    public partial class DetailControl : UserControl
    {
        private PackageManagerControl _control;

        public DetailControl()
        {
            InitializeComponent();
            this.DataContextChanged += PackageSolutionDetailControl_DataContextChanged;
        }

        private void PackageSolutionDetailControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (DataContext is DetailControlModel)
            {
                _root.Visibility = System.Windows.Visibility.Visible;
            }
            else
            {
                _root.Visibility = System.Windows.Visibility.Collapsed;
            }
        }

        private void ExecuteOpenLicenseLink(object sender, ExecutedRoutedEventArgs e)
        {
            Hyperlink hyperlink = e.OriginalSource as Hyperlink;
            if (hyperlink != null && hyperlink.NavigateUri != null)
            {
                Control.Model.UIController.LaunchExternalLink(hyperlink.NavigateUri);
                e.Handled = true;
            }
        }

        public void ScrollToHome()
        {
            _root.ScrollToHome();
        }

        public UserAction GetUserAction()
        {
            var model = (DetailControlModel)DataContext;
            var action = model.SelectedAction == NuGet.PackageManagement.UI.Resources.Action_Uninstall ?
                PackageActionType.Uninstall :
                PackageActionType.Install;

            return new UserAction(
                action,
                new PackageIdentity(model.Id, model.SelectedVersion.Version));
        }

        public void Refresh()
        {
            var model = (DetailControlModel)DataContext;
            if (model != null)
            {
                model.Refresh();
            }
        }

        private void ActionButtonClicked(object sender, RoutedEventArgs e)
        {
            var action = GetUserAction();
            WaitCallback callback = new WaitCallback(async (obj) => 
                await Control.Model.Context.UIActionEngine.PerformAction(Control.Model.UIController, action, this, CancellationToken.None));

            // Run the action using the UIActionEngine on a background thread
            ThreadPool.QueueUserWorkItem(callback, this);
        }

        /// <summary>
        /// Shows the preveiw window for the actions.
        /// </summary>
        /// <param name="actions">actions to preview.</param>
        /// <returns>True if nuget should continue to perform the actions. Otherwise false.</returns>
        private bool PreviewActions(IEnumerable<PreviewResult> actions)
        {
            var w = new PreviewWindow();
            w.DataContext = new PreviewWindowModel(actions);
            return w.ShowModal() == true;
        }

        public FileConflictAction FileConflictAction
        {
            get
            {
                var model = (DetailControlModel)DataContext;
                return model.Options.SelectedFileConflictAction.Action;
            }
        }

        public bool DisplayPreviewWindow
        {
            get
            {
                var model = (DetailControlModel)DataContext;
                return model.Options.ShowPreviewWindow;
            }
        }

        public PackageManagerControl Control
        {
            get
            {
                return _control;
            }

            set
            {
                if (_control == null)
                {
                    // register with the UI controller the first time we get the control model
                    NuGetUI controller = value.Model.UIController as NuGetUI;
                    if (controller != null)
                    {
                        controller.DetailControl = this;
                    }
                }

                _control = value;
            }
        }
    }
}