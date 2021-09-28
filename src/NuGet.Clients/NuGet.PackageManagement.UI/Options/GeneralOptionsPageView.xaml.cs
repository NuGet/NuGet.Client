// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Windows.Controls;

namespace NuGet.Options
{
    /// <summary>
    /// Interaction logic for GeneralOptionsPageView.xaml
    /// </summary>
    public partial class GeneralOptionsPageView : UserControl
    {
        public GeneralOptionsPageView(GeneralOptionsPageViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        }
    }
}
