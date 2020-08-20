// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using NuGet.Versioning;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Interaction logic for ProjectView.xaml. Its DataContext is PackageDetailControlModel.
    /// </summary>
    public partial class ProjectView : UserControl
    {
        public event EventHandler<EventArgs> InstallButtonClicked;
        public event EventHandler<EventArgs> UninstallButtonClicked;

        public ProjectView()
        {
            InitializeComponent();

            // Change ItemContainerStyle of the _versions combobox so that
            // for a null value, a separator is generated.
            var dataTrigger = new DataTrigger();
            dataTrigger.Binding = new Binding();
            dataTrigger.Value = null;
            dataTrigger.Setters.Add(new Setter(TemplateProperty, FindResource("SeparatorControlTemplate")));

            // make sure the separator can't be selected thru keyboard navigation.
            dataTrigger.Setters.Add(new Setter(IsEnabledProperty, false));

            var style = new Style(typeof(ComboBoxItem), _versions.ItemContainerStyle);
            style.Triggers.Add(dataTrigger);
            _versions.ItemContainerStyle = style;
        }

        private void UninstallButton_Clicked(object sender, RoutedEventArgs e)
        {
            if (UninstallButtonClicked != null)
            {
                UninstallButtonClicked(this, EventArgs.Empty);
            }
        }

        //protected TextBox EditableTextBox =>_versions.GetTemplateChild("PART_EditableTextBox") as TextBox;

        private void VersionsKeyUp(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:
                    _versions.IsDropDownOpen = false;
                    break;
                default:
                    var model = (DetailControlModel)DataContext;
                    var userInput = _versions.Text;

                    TextBox textBox1 = _versions.Template.FindName("PART_EditableTextBox", _versions) as TextBox;

                    var isInputValid = VersionRange.TryParse(userInput, true, out VersionRange versionRange);
                    CollectionView itemsViewOriginal = CollectionViewSource.GetDefaultView(_versions.ItemsSource) as CollectionView;
                    itemsViewOriginal.Filter = ((o) =>
                    {
                        if (String.IsNullOrEmpty(_versions.Text)) return true;
                        if (userInput.Length == 0 && userInput.Equals("*", StringComparison.OrdinalIgnoreCase)) return true;
                        else
                        {
                            if (o != null && (o.ToString()).StartsWith(Regex.Replace(_versions.Text, @"[\*]", ""), StringComparison.OrdinalIgnoreCase)) return true;
                            else return false;
                        }
                    });
                    model.UserInput = userInput;
                    textBox1.SelectionStart = userInput.Length;

                    if (userInput == "")
                    {
                        itemsViewOriginal.Refresh();
                        model.UserInput = userInput;
                        textBox1.SelectionStart = userInput.Length;
                    }
                    break;
            }
        }

        private void OpenComboBox(object sender, RoutedEventArgs e)
        {
            CollectionView itemsViewOriginal = CollectionViewSource.GetDefaultView(_versions.ItemsSource) as CollectionView;
            itemsViewOriginal.Refresh();
            _versions.IsDropDownOpen = true;
        }

        private void InstallButton_Clicked(object sender, RoutedEventArgs e)
        {
            var model = DataContext as PackageDetailControlModel;

            if(model.SelectedVersion == null || model.SelectedVersion.Range.OriginalString != _versions.Text)
            {
                var IsValid = VersionRange.TryParse(_versions.Text, out VersionRange versionRange);
                if (IsValid)
                {
                    model.SelectedVersion = new DisplayVersion(VersionRange.Parse(_versions.Text, true), additionalInfo: null);
                }
            }
            if (InstallButtonClicked != null)
            {
                InstallButtonClicked(this, EventArgs.Empty);
            }
        }
    }
}
