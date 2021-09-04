// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.PackageManagement.UI
{
    public class SolutionPickerWindow : VsDialogWindow
    {
        SolutionPickerViewModel _viewModel;

        public SolutionPickerWindow(SolutionPickerViewModel viewModel)
        {
            if (viewModel == null)
            {
                throw new ArgumentNullException(nameof(viewModel));
            }

            _viewModel = viewModel;

            Content = new SolutionPickerView(_viewModel);

            _viewModel.SolutionClicked += ViewModelSolutionClicked;
        }

        public void Dispose()
        {
            _viewModel.SolutionClicked -= ViewModelSolutionClicked;
        }

        private void ViewModelSolutionClicked(object sender, EventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
