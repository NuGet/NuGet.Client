// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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

        private TextBox _textBox;

        private TextBox TextBox
        {
            get
            {
                if (_textBox == null) return _versions.Template.FindName("PART_EditableTextBox", _versions) as TextBox;
                return _textBox;
            }
            set
            {
                _textBox = value;
            }
        }

        private PackageDetailControlModel _packageDetailControlModel;

        private PackageDetailControlModel PackageDetailControlModel
        {
            get
            {
                if (_packageDetailControlModel == null) return (PackageDetailControlModel)DataContext;
                return _packageDetailControlModel;
            }
            set
            {
                _packageDetailControlModel = value;
            }
        }

        private DetailControlModel _detailModel;

        private DetailControlModel DetailModel
        {
            get
            {
                if (_detailModel == null) return (DetailControlModel)DataContext;
                return _detailModel;
            }
            set
            {
                _detailModel = value;
            }
        }

        private string _previousFilter;
        private string PreviousText
        {
            get
            {
                if (_previousFilter != null) return _previousFilter;
                return string.Empty;
            }
            set
            {
                _previousFilter = value;
            }
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (PackageDetailControlModel.IsProjectPackageReference)
            {
                var comboboxText = _versions.Text;

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
                    case Key.Down:
                        if (_versions.SelectedIndex < _versions.Items.Count - 1)
                        {
                            // Handle null separator
                            if (_versions.Items[_versions.SelectedIndex + 1] == null)
                            {
                                _versions.SelectedIndex = _versions.SelectedIndex + 2;
                            }
                            else
                            {
                                _versions.SelectedIndex++;
                            }

                            // Changing the SelectedIndex will change the text
                            _versions.Text = comboboxText;
                            TextBox.SelectionStart = comboboxText.Length;

                            e.Handled = true;
                        }
                        break;
                    case Key.Up:
                        if (_versions.SelectedIndex > 0)
                        {
                            // Handle null separator
                            if (_versions.Items[_versions.SelectedIndex - 1] == null)
                            {
                                _versions.SelectedIndex = _versions.SelectedIndex - 2;
                            }
                            else
                            {
                                _versions.SelectedIndex--;
                            }

                            // Changing the SelectedIndex will change the text
                            _versions.Text = comboboxText;
                            TextBox.SelectionStart = comboboxText.Length;

                            e.Handled = true;
                        }
                        break;
                    default:
                        break;
                }
            }
            else
            {
                base.OnPreviewKeyDown(e);
            }
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (PackageDetailControlModel.IsProjectPackageReference)
            {
                string comboboxText = _versions.Text;
                IEnumerable<NuGetVersion> versions = DetailModel.Versions.Where(v => v != null).Select(v => v.Version);
                bool userTypedAVersionRange = comboboxText.StartsWith("(", StringComparison.OrdinalIgnoreCase) || comboboxText.StartsWith("[", StringComparison.OrdinalIgnoreCase);

                switch (e.Key)
                {
                    case Key.Enter:
                        VersionRange userRequestedVersionRange = null;
                        bool isAVersion = _versions.Items.CurrentItem != null;
                        bool isUserInputValidNuGetVersionRange = _versions.SelectedIndex >= 0 ? VersionRange.TryParse(comboboxText, out userRequestedVersionRange) : false;

                        if (_versions.SelectedIndex >= 0 && isAVersion)
                        {
                            // Check if the user typed a custom version
                            if (userRequestedVersionRange != null && (userRequestedVersionRange.IsFloating || userTypedAVersionRange))
                            {
                                // Search for the best version
                                NuGetVersion rangeBestVersion = userRequestedVersionRange.FindBestMatch(versions);
                                bool isBestOption = rangeBestVersion.ToString() == _versions.Items[_versions.SelectedIndex].ToString();
                                if (isBestOption)
                                {
                                    PackageDetailControlModel.SelectedVersion = new DisplayVersion(userRequestedVersionRange, rangeBestVersion, additionalInfo: null);
                                    _versions.Text = comboboxText;
                                }
                                else
                                {
                                    PackageDetailControlModel.SelectedVersion = _versions.Items[_versions.SelectedIndex] as DisplayVersion;
                                    _versions.Text = PackageDetailControlModel.SelectedVersion.ToString();
                                }
                            }
                            else
                            {
                                PackageDetailControlModel.SelectedVersion = _versions.Items[_versions.SelectedIndex] as DisplayVersion;
                                _versions.Text = PackageDetailControlModel.SelectedVersion.ToString();
                            }

                            TextBox.SelectionStart = 0;
                            TextBox.SelectionLength = int.MaxValue;
                            _versions.IsDropDownOpen = false;
                        }

                        e.Handled = true;
                        break;
                    case Key.Down:
                    case Key.Up:
                        e.Handled = true;
                        break;
                    default:
                        if (PreviousText != comboboxText)
                        {
                            PreviousText = comboboxText;
                            var selectionStart = TextBox.SelectionStart;

                            NuGetVersion matchVersion = null;
                            VersionRange userRange = null;

                            // Get the best version from the range if the user typed a custom version
                            bool userTypedAValidVersionRange = VersionRange.TryParse(comboboxText, out userRange);
                            if (userTypedAValidVersionRange && (userTypedAVersionRange || (userRange != null && userRange.IsFloating)))
                            {
                                matchVersion = userRange.FindBestMatch(versions);
                            }

                            // If the selected version is not the correct one, deselect a version so Install/Update button is disabled.
                            if (_versions.SelectedIndex != -1 &&
                                (_versions.Text != _versions.Items[_versions.SelectedIndex].ToString() || matchVersion?.ToString() != _versions.Items[_versions.SelectedIndex].ToString()))
                            {
                                _versions.SelectedIndex = -1;
                                PackageDetailControlModel.SelectedVersion = null;
                            }

                            // Automatically select the item when the input or custom range text matches it
                            for (int i = 0; i < _versions.Items.Count; i++)
                            {
                                if (_versions.Items[i] != null && (comboboxText == _versions.Items[i].ToString() || _versions.Items[i].ToString() == matchVersion?.ToString()))
                                {
                                    _versions.SelectedIndex = i;
                                }
                            }

                            _versions.Text = comboboxText;
                            TextBox.SelectionStart = selectionStart;

                            break;
                        }

                        base.OnKeyUp(e);
                        break;
                }
            }
            else
            {
                base.OnKeyUp(e);
            }
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
            if (InstallButtonClicked != null)
            {
                InstallButtonClicked(this, EventArgs.Empty);
            }
        }
    }
}
