// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace NuGet.PackageManagement.UI
{
    public class SolutionPickerItemViewModel
    {
        public SolutionPickerItemViewModel(ICommand command, string filePath)
        {
            OpenSolutionCommand = command;
            FilePath = filePath;
        }

        public ICommand OpenSolutionCommand { get; }
        public string FilePath { get; }
    }
}
