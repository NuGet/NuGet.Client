// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;

namespace NuGet.PackageManagement.UI
{
    public class SolutionPickerWindow : VsDialogWindow
    {
        public SolutionPickerWindow(SolutionPickerViewModel viewModel)
        {
            Content = new SolutionPickerView(viewModel);
            Closing += new CancelEventHandler(Window_ClosingAction);
        }

        private void Window_ClosingAction(object sender, CancelEventArgs e)
        {
            if (DialogResult != true)
            {
                DataContext = false;
                var dte = Package.GetGlobalService(typeof(_DTE)) as DTE2;
                dte.ExecuteCommand("File.Exit");
            }
        }
    }
}
