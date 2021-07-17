// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Interaction logic for OpenReadMeMarkdownView.xaml
    /// </summary>
    public partial class OpenReadMeMarkdownView : UserControl
    {

        public OpenReadMeMarkdownView(OpenReadMeMarkdownViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void OpenHyperlink(object sender, ExecutedRoutedEventArgs e)
        {
            Process.Start(e.Parameter.ToString());
        }

        private void ClickOnImage(object sender, ExecutedRoutedEventArgs e)
        {
            MessageBox.Show($"URL: {e.Parameter}");
        }
    }
}
