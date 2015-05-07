// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;

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
            _projectList.MaxHeight = _self.FontSize * 15;
            DataContextChanged += PackageSolutionDetailControl_DataContextChanged;
        }

        private void PackageSolutionDetailControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (DataContext is DetailControlModel)
            {
                _root.Visibility = Visibility.Visible;
            }
            else
            {
                _root.Visibility = Visibility.Collapsed;
            }
        }

        private void ExecuteOpenLicenseLink(object sender, ExecutedRoutedEventArgs e)
        {
            var hyperlink = e.OriginalSource as Hyperlink;
            if (hyperlink != null
                && hyperlink.NavigateUri != null)
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
            var action = model.SelectedAction == UI.Resources.Action_Uninstall ?
                NuGetProjectActionType.Uninstall :
                NuGetProjectActionType.Install;

            return new UserAction(
                action,
                model.Id,
                model.SelectedVersion != null ? model.SelectedVersion.Version : null);
        }

        public void Refresh()
        {
            var model = DataContext as DetailControlModel;
            if (model != null)
            {
                model.Refresh();
            }
        }

        private void ActionButtonClicked(object sender, RoutedEventArgs e)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async delegate
                {
                    var action = GetUserAction();
                    Control.IsEnabled = false;
                    NuGetEventTrigger.Instance.TriggerEvent(NuGetEvent.PackageOperationBegin);
                    try
                    {
                        await Task.Run(() =>
                            Control.Model.Context.UIActionEngine.PerformActionAsync(
                                Control.Model.UIController,
                                action,
                                this,
                                CancellationToken.None));
                    }
                    finally
                    {
                        NuGetEventTrigger.Instance.TriggerEvent(NuGetEvent.PackageOperationEnd);
                        Control.IsEnabled = true;
                    }
                });
        }

        public PackageManagerControl Control
        {
            get { return _control; }

            set
            {
                if (_control == null)
                {
                    // register with the UI controller the first time we get the control model
                    var controller = value.Model.UIController as NuGetUI;
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
