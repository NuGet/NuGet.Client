// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Windows;
using NuGet.PackageManagement.VisualStudio;

namespace NuGet.PackageManagement.UI
{
    public class SolutionPickerWindow : VsDialogWindow
    {
        public SolutionPickerWindow(SolutionPickerViewModel viewModel)
        {
            if (viewModel == null)
            {
                throw new ArgumentNullException(nameof(viewModel));
            }

            Content = new SolutionPickerView(viewModel);

            EventHandler handler = null;
            handler = (s, e) =>
            {
                viewModel.SolutionClicked -= handler;
                DialogResult = true;
            };
            viewModel.SolutionClicked += handler;
        }
    }
}
