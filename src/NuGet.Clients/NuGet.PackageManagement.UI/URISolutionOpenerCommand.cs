// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Windows.Input;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;

namespace NuGet.PackageManagement.UI
{
    public class URISolutionOpenerCommand : ICommand
    {
        //never raised
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter)
        {
            return true;
        }

        public void Execute(object parameter)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var filePath = parameter.ToString();
            if (File.Exists(filePath))
            {
                var dte = Package.GetGlobalService(typeof(_DTE)) as DTE2;
                dte.Solution.Open(filePath);
            }
        }
    }
}
