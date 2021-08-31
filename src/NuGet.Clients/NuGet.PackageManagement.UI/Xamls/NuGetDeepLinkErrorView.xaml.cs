// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Windows;
using System.Windows.Controls;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Interaction logic for NuGetDeepLinkErrorView.xaml
    /// </summary>
    public partial class NuGetDeepLinkErrorView : UserControl
    {
        public NuGetDeepLinkErrorView(string message, string buttonText)
        {
            InitializeComponent();
            ErrorWindowMessage = message;
            ButtonText = buttonText;
            DataContext = this;
        }

        public string ErrorWindowMessage { get; }
        public string ButtonText { get; }
    }
}
