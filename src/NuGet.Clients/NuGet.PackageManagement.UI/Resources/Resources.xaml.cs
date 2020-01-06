// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace NuGet.PackageManagement.UI
{
    internal partial class SharedResources : ResourceDictionary
    {
        public SharedResources()
        {
            InitializeComponent();
        }

        private void PackageIconImage_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            var image = sender as Image;

            e.Handled = true; // don't repropagate the event
            var binding = BindingOperations.GetBindingExpression(image, Image.SourceProperty);
            binding.UpdateTarget();
        }
    }
}
