// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Windows;
using System.Windows.Controls;
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

        private string _formatStringSingle;
        public string FormatStringSingle
        {
            get => _formatStringSingle;

            set
            {
                if (_formatStringSingle != value)
                {
                    _formatStringSingle = value;
                    FillTextBlock();
                }
            }
        }

        private string _formatStringAlternative;
        public string FormatStringAlternative
        {
            get => _formatStringAlternative;

            set
            {
                if (_formatStringAlternative != value)
                {
                    _formatStringAlternative = value;
                    FillTextBlock();
                }
            }
        }

        private void UserControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            FillTextBlock();
        }

        private void FillTextBlock()
        {
            var deprecationMeta = DataContext as PackageDeprecationMetadataContextInfo;

            if (deprecationMeta != null)
            {
                if (deprecationMeta.AlternatePackage != null)
                {
                    AlternatePackageMetadataContextInfo alternatePackage = deprecationMeta.AlternatePackage;
                    int index = FormatStringAlternative.IndexOf("{0}", StringComparison.Ordinal);

                    if (index != -1)
                    {
                        string begin = FormatStringAlternative.Substring(0, index);
                        string end = FormatStringAlternative.Substring(index + "{0}".Length);

                        var link = new Hyperlink(new Run(alternatePackage.PackageId))
                        {
                            ToolTip = UI.Resources.Deprecation_LinkTooltip,
                            Command = Commands.SearchPackageCommand,
                            CommandParameter = alternatePackage.PackageId,
                            Style = (Style)Resources["HyperlinkStyleNoUri"],
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
