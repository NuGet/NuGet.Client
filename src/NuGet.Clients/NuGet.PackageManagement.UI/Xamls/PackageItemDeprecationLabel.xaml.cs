// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Interaction logic for PackageItemDeprecationLabel.xaml
    ///
    /// DataContext is <see cref="PackageDeprecationMetadataContextInfo"/>
    /// 
    /// Similar to <see cref="PackageManagerProvidersLabel"/>
    /// </summary>
    public partial class PackageItemDeprecationLabel : UserControl
    {
        public PackageItemDeprecationLabel()
        {
            InitializeComponent();
        }

        public string FormatStringSingle { get; set; }

        public string FormatStringAlternative { get; set; }

        public string LeftPart
        {
            get
            {
                int index = FormatStringAlternative.IndexOf("{0}", StringComparison.Ordinal);
                string begin = FormatStringAlternative.Substring(0, index);
                return begin;
            }
            set { }
        }

        public string RightPart
        {
            get
            {
                int index = FormatStringAlternative.IndexOf("{0}", StringComparison.Ordinal);
                string end = FormatStringAlternative.Substring(index + "{0}".Length);
                return end;
            }
            set { }
        }
    }
}
