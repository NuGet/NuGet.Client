// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.VisualStudio.Shell;

namespace NuGet.Options
{
    [SuppressMessage(
        "Microsoft.Interoperability",
        "CA1408:DoNotUseAutoDualClassInterfaceType")]
    [Guid("2819C3B6-FC75-4CD5-8C77-877903DE864C")]
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public class PackageSourceOptionsPage : OptionsPageBase
    {
        private PackageSourcesOptionsControl _optionsWindow;

        protected override void OnActivate(CancelEventArgs e)
        {
            base.OnActivate(e);
            PackageSourcesControl.Font = VsShellUtilities.GetEnvironmentFont(this);
            PackageSourcesControl.InitializeOnActivated();
        }

        protected override void OnApply(PageApplyEventArgs e)
        {
            // Do not need to call base.OnApply() here.
            bool wasApplied = PackageSourcesControl.ApplyChangedSettings();
            if (!wasApplied)
            {
                e.ApplyBehavior = ApplyKind.CancelNoNavigate;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            PackageSourcesControl.ClearSettings();
            base.OnClosed(e);
        }

        private PackageSourcesOptionsControl PackageSourcesControl
        {
            get
            {
                if (_optionsWindow == null)
                {
                    _optionsWindow = new PackageSourcesOptionsControl(this);
                    _optionsWindow.Location = new Point(0, 0);
                }

                return _optionsWindow;
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        protected override IWin32Window Window
        {
            get { return PackageSourcesControl; }
        }
    }
}
