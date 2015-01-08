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

namespace NuGet.PackageManagement.UI
{
    // The DataContext of this control is DetailControlModel, i.e. either 
    // PackageSolutionDetailControlModel or PackageDetailControlModel.
    public partial class DetailControl : UserControl
    {
        public PackageManagerControl Control { get; set; }

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
                Control.UI.LaunchExternalLink(hyperlink.NavigateUri);
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
            Control.PerformAction(this);
        }

        public FileConflictAction FileConflictAction
        {
            get
            {
                var model = (DetailControlModel)DataContext;
                return model.Options.SelectedFileConflictAction.Action;
            }
        }
    }
}