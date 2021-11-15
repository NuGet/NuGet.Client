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

        private string _previousFilter;
        private string PreviousText
        {
            get
            {
                if (_previousFilter != null) return _previousFilter;
                return "";
            }
            set
            {
                _previousFilter = value;
            }
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            PackageDetailControlModel model = (PackageDetailControlModel)DataContext;
            if (model.IsProjectPackageReference)
            {
                TextBox textBox = _versions.Template.FindName("PART_EditableTextBox", _versions) as TextBox;
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

                            _versions.Text = comboboxText;
                            textBox.SelectionStart = comboboxText.Length;

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

                            _versions.Text = comboboxText;
                            textBox.SelectionStart = comboboxText.Length;

                            e.Handled = true;
                        }
                        break;
                    case Key.Back:
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
            PackageDetailControlModel packageDetailControlModel = (PackageDetailControlModel)DataContext;
            if (packageDetailControlModel.IsProjectPackageReference)
            {
                TextBox textBox = _versions.Template.FindName("PART_EditableTextBox", _versions) as TextBox;
                string comboboxText = _versions.Text;

                DetailControlModel model = (DetailControlModel)DataContext;
                VersionRange userRequestedVersionRange = null;

                IEnumerable<NuGetVersion> versions = model.Versions.Where(v => v != null).Select(v => v.Version);
                bool userTypedAVersionRange = comboboxText.StartsWith("(", StringComparison.OrdinalIgnoreCase) || comboboxText.StartsWith("[", StringComparison.OrdinalIgnoreCase);

                switch (e.Key)
                {
                    case Key.Enter:
                        bool isAVersion = _versions.Items.CurrentItem != null;
                        bool isUserInputValidNuGetVersionRange = _versions.SelectedIndex >= 0 ? VersionRange.TryParse(comboboxText, out userRequestedVersionRange) : false;

                        if (_versions.SelectedIndex >= 0 && isAVersion)
                        {
                            if (userRequestedVersionRange != null && (userRequestedVersionRange.IsFloating || userTypedAVersionRange))
                            {
                                NuGetVersion rangeBestVersion = userRequestedVersionRange.FindBestMatch(versions);
                                bool isBestOption = rangeBestVersion.ToString() == _versions.Items[_versions.SelectedIndex].ToString();
                                if (isBestOption)
                                {
                                    packageDetailControlModel.SelectedVersion = new DisplayVersion(userRequestedVersionRange, rangeBestVersion, additionalInfo: null);
                                    _versions.Text = comboboxText;
                                }
                                else
                                {
                                    packageDetailControlModel.SelectedVersion = _versions.Items[_versions.SelectedIndex] as DisplayVersion;
                                    _versions.Text = packageDetailControlModel.SelectedVersion.ToString();
                                }
                            }
                            else
                            {
                                packageDetailControlModel.SelectedVersion = _versions.Items[_versions.SelectedIndex] as DisplayVersion;
                                _versions.Text = packageDetailControlModel.SelectedVersion.ToString();
                            }

                            textBox.SelectionStart = 0;
                            textBox.SelectionLength = int.MaxValue;
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
                            var selectionStart = textBox.SelectionStart;

                            NuGetVersion matchVersion = null;
                            VersionRange userRange = null;

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
                                packageDetailControlModel.SelectedVersion = null;
                            }

                            // Automatically select the item when the input or custom range text matches it
                            for (int i = 0; i < _versions.Items.Count; i++)
                            {
                                if (_versions.Items[i] != null && (comboboxText == _versions.Items[i].ToString() || _versions.Items[i].ToString() == matchVersion?.ToString()))
                                {
                                    _versions.SelectedIndex = i;
                                }
                            }

                            if (_versions.SelectedIndex == -1)
                            {
                                packageDetailControlModel.SelectedVersion = null;
                            }

                            _versions.Text = comboboxText;
                            textBox.SelectionStart = selectionStart;

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
