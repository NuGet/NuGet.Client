// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Interaction logic for PackageManagerProvidersLabel.xaml. Its DataContext is
    /// PackageItemListViewModel.
    /// </summary>
    public partial class PackageManagerProvidersLabel : UserControl
    {
        public PackageManagerProvidersLabel()
        {
            InitializeComponent();

            this.DataContextChanged += PackageManagerProvidersLabel_DataContextChanged;
        }

        public string FormatString
        {
            get;
            set;
        }

        private void PackageManagerProvidersLabel_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            _textBlock.Inlines.Clear();

            var providers = DataContext as AlternativePackageManagerProviders;
            if (providers == null || providers.PackageManagerProviders.IsEmpty())
            {
                Visibility = Visibility.Collapsed;
                return;
            }

            // Processing the format string ourselves. We only support "{0}".
            string begin = string.Empty;
            string end = string.Empty;
            if (!string.IsNullOrEmpty(FormatString))
            {
                var index = FormatString.IndexOf("{0}");

                if (index == -1)
                {
                    // Cannot find "{0}".
                    Debug.Fail("Label_ConsiderUsing does not contain {0}");
                    begin = FormatString;
                }
                else
                {
                    begin = FormatString.Substring(0, index);
                    end = FormatString.Substring(index + "{0}".Length);
                }
            }

            _textBlock.Inlines.Add(new Run(begin));

            // Generate the hyperlinks for providers
            bool firstElement = true;
            foreach (var provider in providers.PackageManagerProviders)
            {
                if (firstElement)
                {
                    firstElement = false;
                }
                else
                {
                    _textBlock.Inlines.Add(new Run(", "));
                }

                var hyperLink = new Hyperlink(new Run(provider.PackageManagerName));
                hyperLink.ToolTip = provider.Description;
                hyperLink.Click += (_, __) =>
                {
                    provider.GoToPackage(providers.PackageId, providers.ProjectName);
                };
                _textBlock.Inlines.Add(hyperLink);
            }

            _textBlock.Inlines.Add(new Run(end));
            Visibility = Visibility.Visible;
        }
    }
}