// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using System.Runtime.InteropServices;
using System.ComponentModel.Design;
using Microsoft.VisualStudio.Shell.Interop;
using System.Windows.Controls;

namespace NuGet.PackageManagement.UI
{
    [Guid(Constants.ReadMeMarkdownToolWindowGuid)]
    public class ReadMeMarkdownToolWindow : ToolWindowPane
    {
        //private Dictionary<Guid, ProvideToolWindowAttribute> _toolWindowTypes = new Dictionary<Guid, ProvideToolWindowAttribute>();
        public ReadMeMarkdownToolWindow() : base(null)
        {
            Caption = "README.md";// TODO caption for next time
            Content = new UserControl();
            //_toolWindowTypes.Add(Guid("bcd11f28-784f-44fd-b9fc-d5c1f4e1b96f"), "5fcc8577-4feb-4d04-ad72-d6c629b083cc");// TODO: fix numbers, they are not correct
        }

    }
}

