// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Windows.Input;

namespace NuGet.PackageManagement.UI
{
    public class SolutionPickerItemViewModel
    {
        public SolutionPickerItemViewModel(ICommand command, string filePath)
        {
            OpenSolutionCommand = command;
            FullPath = filePath;
            Directory = Path.GetDirectoryName(filePath);
            SolutionName = Path.GetFileName(filePath);
        }

        public ICommand OpenSolutionCommand { get; }

        public string Directory { get; }

        public string SolutionName { get; }

        public string FullPath { get; }
    }
}
