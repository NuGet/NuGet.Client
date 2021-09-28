// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.VisualStudio.Shell;
using NuGet.Commands;
using NuGet.Configuration;
using NuGet.PackageManagement;
using NuGet.PackageManagement.VisualStudio;
using NuGet.VisualStudio;

namespace NuGet.Options
{
    [Guid(PageId)]
    public sealed class GeneralOptionsPage : UIElementDialogPage
    {
        public const string PageId = "9A9BC6E5-E2E3-4DB3-BA9E-2A4C6A409276";

        private GeneralOptionsPageView _view;
        private GeneralOptionsPageViewModel _viewModel;

        protected override UIElement Child => GetView();

        private GeneralOptionsPageView GetView()
        {
            if (_view == null)
            {
                var settings = ServiceLocator.GetComponentModelService<ISettings>();
                var packageRestoreConsent = new PackageRestoreConsent(settings);
                var packageManagementFormat = new PackageManagementFormat(settings);
                var bindingRedirectBehavior = new BindingRedirectBehavior(settings);

                var outputConsoleLogger = ServiceLocator.GetComponentModelService<INuGetUILogger>();
                var localsCommandRunner = new LocalsCommandRunner();

                _viewModel = new GeneralOptionsPageViewModel(
                    packageRestoreConsent,
                    packageManagementFormat,
                    bindingRedirectBehavior,
                    localsCommandRunner,
                    outputConsoleLogger);
                _view = new GeneralOptionsPageView(_viewModel);
            }

            return _view;
        }
    }
}
