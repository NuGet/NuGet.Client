// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Interaction logic for PackageItemDeprecationLabel.xaml
    ///
    /// This is very similar to <see cref="PackageManagerProvidersLabel"/>
    /// </summary>
    public partial class PackageItemDeprecationLabel : UserControl
    {
        public PackageItemDeprecationLabel()
        {
            InitializeComponent();
        }

        public string FormatStringSingle { get { return "The package is deprecated."; } }

        public string FormatString
        {
            get { return "The package is deprecated. Use {0} instead"; }
            set { FillTexts(); }
        }

        private void UserControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            FillTexts();
        }

        private void FillTexts()
        {
            var deprecationMeta = DataContext as PackageDeprecationMetadataContextInfo;

            if (deprecationMeta != null)
            {
                if (deprecationMeta.AlternatePackage != null)
                {
                    var alternatePackage = deprecationMeta.AlternatePackage;
                    var index = FormatString.IndexOf("{0}", StringComparison.Ordinal);

                    if (index != -1)
                    {
                        var begin = FormatString.Substring(0, index);
                        var end = FormatString.Substring(index + "{0}".Length);
                        var linkBuilder = new UriBuilder("nugetpm", alternatePackage.PackageId);

                        var link = new Hyperlink(new Run(alternatePackage.PackageId))
                        {
                            ToolTip = "Click to search package",
                            Command = PackageManagerControlCommands.MakeSearchLink,
                            NavigateUri = linkBuilder.Uri,
                        };

                        _textBlock.Inlines.Clear();
                        _textBlock.Inlines.Add(new Run(begin));
                        _textBlock.Inlines.Add(link);
                        _textBlock.Inlines.Add(new Run(end));
                        Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    _textBlock.Inlines.Clear();
                    _textBlock.Inlines.Add(new Run(FormatStringSingle));
                    Visibility = Visibility.Visible;
                }
            }
            else
            {
                _textBlock.Inlines.Clear();
                Visibility = Visibility.Collapsed;
            }
        }
    }
}
