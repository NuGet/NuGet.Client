// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
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

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

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

        private string _previousFilter;
        private string PreviousFilter
        {
            get
            {
                if (_previousFilter != null) return _previousFilter;
                return _versions.SelectedValue.ToString();
            }
            set
            {
                _previousFilter = value;
                OnPropertyChanged(nameof(PreviousFilter));
            }
        }

        private bool VersionsFilter(object o)
        {
            string ComboboxText = _versions.Text;
            if (string.IsNullOrEmpty(_versions.Text)) return true;
            if (ComboboxText.Length == 0 && ComboboxText.Equals("*", StringComparison.OrdinalIgnoreCase)) return true;
            if ((ComboboxText.StartsWith("(", StringComparison.OrdinalIgnoreCase) || ComboboxText.StartsWith("[", StringComparison.OrdinalIgnoreCase)) &&
               VersionRange.TryParse(ComboboxText, out VersionRange userRange))
            {
                if (o != null && NuGetVersion.TryParse(o.ToString(), out NuGetVersion userVersion))
                {
                    if (userRange.Satisfies(userVersion))
                    {
                        return true;
                    }
                    else { return false; }
                }
                else
                {
                    return false;
                }
            }
            else
            {
                if (o != null && (o.ToString()).StartsWith(Regex.Replace(_versions.Text, @"[\*]", ""), StringComparison.OrdinalIgnoreCase)) return true;
                else return false;
            }
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Tab:
                case Key.Enter:
                    _versions.IsDropDownOpen = false;
                    break;
                case Key.Escape:
                    _versions.IsDropDownOpen = false;
                    _versions.SelectedIndex = -1;
                    break;
                case Key.Up:
                case Key.Down:
                    if (e.Key == Key.Down) _versions.IsDropDownOpen = true;
                    break;
                case Key.Back:
                    break;
                default:
                    base.OnPreviewKeyDown(e);
                    break;
            }
        }


        protected override void OnKeyUp(KeyEventArgs e)
        {
            _versions.IsDropDownOpen = true;
            TextBox EditableTextBox = _versions.Template.FindName("PART_EditableTextBox", _versions) as TextBox;
            string ComboboxText = _versions.Text;

            DetailControlModel model = (DetailControlModel)DataContext;
            VersionRange userRequestedVersionRange = null;

            IEnumerable<NuGetVersion> versions = model.Versions.Where(v => v != null).Select(v => v.Version);
            bool userTypedAVersionRange = ComboboxText.StartsWith("(", StringComparison.OrdinalIgnoreCase) || ComboboxText.StartsWith("[", StringComparison.OrdinalIgnoreCase);

            switch (e.Key)
            {
                case Key.Enter:
                    bool isValidNuGetVersionRange = _versions.SelectedIndex >= 0 ? VersionRange.TryParse(_versions.Items[_versions.SelectedIndex].ToString(), out VersionRange requestedVersionRange) : false;
                    bool isUserInputValidNuGetVersionRange = _versions.SelectedIndex >= 0 ? VersionRange.TryParse(ComboboxText, out userRequestedVersionRange) : false;

                    if (_versions.SelectedIndex >= 0 && isValidNuGetVersionRange)
                    {
                        if (userTypedAVersionRange)
                        {
                            NuGetVersion rangeBestVersion = userRequestedVersionRange.FindBestMatch(versions);
                            bool isBestOption = rangeBestVersion.ToString() == _versions.Items[_versions.SelectedIndex].ToString();
                            if (isBestOption)
                            {
                                _versions.SelectedValue = new DisplayVersion(userRequestedVersionRange, rangeBestVersion, additionalInfo: null);
                                _versions.SelectedIndex = -1;
                                _versions.Text = ComboboxText;
                                EditableTextBox.SelectionStart = 0;
                                EditableTextBox.SelectionLength = int.MaxValue;
                                _versions.IsDropDownOpen = false;

                                e.Handled = true;
                                break;
                            }
                        }
                        _versions.SelectedValue = _versions.Items[_versions.SelectedIndex];
                        _versions.Text = _versions.SelectedValue.ToString();
                        EditableTextBox.SelectionStart = 0;
                        EditableTextBox.SelectionLength = int.MaxValue;
                    }

                    _versions.IsDropDownOpen = false;
                    e.Handled = true;
                    break;
                case Key.Down:
                    if (_versions.SelectedIndex < _versions.Items.Count - 1)
                    {
                        // The separator is a null
                        if (_versions.SelectedItem == null)
                        {
                            _versions.SelectedIndex++;
                        }
                        e.Handled = true;
                    }
                    break;
                case Key.Up:
                    if (_versions.SelectedIndex >= 0)
                    {
                        // The separator is a null
                        if (_versions.SelectedItem == null)
                        {
                            _versions.SelectedIndex++;
                        }
                        e.Handled = true;
                    }
                    break;
                case Key.Left:
                case Key.Right:
                    base.OnKeyUp(e);
                    break;
                default:
                    if (PreviousFilter != ComboboxText)
                    {
                        PreviousFilter = ComboboxText;
                        var SelectionPosition = EditableTextBox.SelectionStart;

                        if (_versions.SelectedIndex != -1 && _versions.Text != _versions.Items[_versions.SelectedIndex].ToString())
                        {
                            _versions.SelectedIndex = -1; // Clear selection. This line will also clear Text property
                            _versions.Text = ComboboxText;
                            EditableTextBox.SelectionStart = SelectionPosition;
                        }

                        // This filtering needs to be in the initilization
                        CollectionView itemsViewOriginal = CollectionViewSource.GetDefaultView(_versions.ItemsSource) as CollectionView;
                        itemsViewOriginal.Filter = VersionsFilter;

                        if (ComboboxText == "")
                        {
                            itemsViewOriginal.Refresh();
                            break;
                        }

                        bool userTypedAValidVersionRange = VersionRange.TryParse(ComboboxText, out VersionRange userRange);

                        NuGetVersion matchVersion = null;
                        if (userTypedAValidVersionRange && versions != null)
                        {
                            matchVersion = userRange.FindBestMatch(versions);
                        }

                        // Automatically select the item when the input text matches it
                        for (int i = 0; i < _versions.Items.Count; i++)
                        {
                            if (_versions.Items[i] != null && (_versions.Text == _versions.Items[i].ToString() || _versions.Items[i].ToString() == matchVersion?.ToString()))
                            {
                                _versions.SelectedIndex = i;
                                _versions.SelectedValue = _versions.Items[i];
                            }
                        }

                        _versions.Text = ComboboxText;
                        EditableTextBox.SelectionStart = SelectionPosition;

                        break;
                    }

                    e.Handled = true;
                    break;
            }
        }

        private void OpenComboBox(object sender, RoutedEventArgs e)
        {
            CollectionView itemsViewOriginal = CollectionViewSource.GetDefaultView(_versions.ItemsSource) as CollectionView;
            itemsViewOriginal.Refresh();

            _versions.IsDropDownOpen = true;
        }

        private void UninstallButton_Clicked(object sender, RoutedEventArgs e)
        {
            if (UninstallButtonClicked != null)
            {
                UninstallButtonClicked(this, EventArgs.Empty);
            }
        }

        private void InstallButton_Clicked(object sender, RoutedEventArgs e)
        {
            var model = DataContext as PackageDetailControlModel;
            string ComboboxText = _versions.Text;

            if (model.SelectedVersion == null || model.SelectedVersion.Range.OriginalString != _versions.Text)
            {
                var IsValid = VersionRange.TryParse(_versions.Text, out VersionRange versionRange);
                if (IsValid)
                {
                    model.SelectedVersion = new DisplayVersion(VersionRange.Parse(_versions.Text, true), additionalInfo: null);
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedVersion"));
                    _versions.Text = ComboboxText;
                }
            }
            if (InstallButtonClicked != null)
            {
                bool IsValid = VersionRange.TryParse(_versions.Text, out VersionRange versionRange);
                if (IsValid)
                {
                    model.SelectedVersion = new DisplayVersion(versionRange, additionalInfo: null);
                }
            }

            InstallButtonClicked?.Invoke(this, EventArgs.Empty);
        }
    }
}
