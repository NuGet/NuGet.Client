// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.Common;
using NuGet.PackageManagement.UI;
using NuGet.Protocol.Core.Types;
using NuGet.VisualStudio;

namespace NuGet.Options
{
    /// <summary>
    /// Represents the Tools - Options - Package Manager dialog
    /// </summary>
    /// <remarks>
    /// The code in this class assumes that while the dialog is open, noone is modifying the
    /// VSPackageSourceProvider directly.
    /// Otherwise, we have a problem with synchronization with the package source provider.
    /// </remarks>
    public partial class PackageSourcesOptionsControl : UserControl
    {
        private readonly Configuration.IPackageSourceProvider _packageSourceProvider;
        private BindingSource _packageSources;
        private BindingSource _machineWidepackageSources;
        private readonly IServiceProvider _serviceProvider;
        private bool _initialized;
        private Size _checkBoxSize;

        public PackageSourcesOptionsControl(IServiceProvider serviceProvider)
            : this(ServiceLocator.GetInstance<ISourceRepositoryProvider>(), serviceProvider)
        {
        }

        public PackageSourcesOptionsControl(ISourceRepositoryProvider sourceRepositoryProvider, IServiceProvider serviceProvider)
        {
            InitializeComponent();

            if (sourceRepositoryProvider == null)
            {
                throw new ArgumentNullException("sourceRepositoryProvider");
            }

            _serviceProvider = serviceProvider;
            _packageSourceProvider = sourceRepositoryProvider.PackageSourceProvider;
            SetupEventHandlers();

            UpdateDPI();
        }

        private void UpdateDPI()
        {
            var imgs = images16px;
            if (addButton.Height > 72)
            {
                imgs = images64px;
            }
            else if (addButton.Height > 40)
            {
                imgs = images32px;
            }

            addButton.ImageList = imgs;
            removeButton.ImageList = imgs;
            MoveUpButton.ImageList = imgs;
            MoveDownButton.ImageList = imgs;
        }

        private void SetupEventHandlers()
        {
            NewPackageName.TextChanged += (o, e) => UpdateUI();
            NewPackageSource.TextChanged += (o, e) => UpdateUI();
            MoveUpButton.Click += (o, e) => MoveSelectedItem(-1);
            MoveDownButton.Click += (o, e) => MoveSelectedItem(1);
            NewPackageName.Focus();
            UpdateUI();
        }

        private void UpdateUI()
        {
            // It is only allowed for 1 of the listboxes to be selected at any time or neither
            // Never MUST both the listboxes be selected
            Debug.Assert(PackageSourcesListBox.SelectedItem == null || MachineWidePackageSourcesListBox.SelectedItem == null);

            var selectedSource = (Configuration.PackageSource)PackageSourcesListBox.SelectedItem;
            var selectedMachineSource = (Configuration.PackageSource)MachineWidePackageSourcesListBox.SelectedItem;

            if (selectedMachineSource != null)
            {
                // This block corresponds to MachineWidePackageSourcesListBox
                addButton.Enabled = false;
                removeButton.Enabled = false;
                MoveUpButton.Enabled = false;
                MoveDownButton.Enabled = false;
                BrowseButton.Enabled = false;
                updateButton.Enabled = false;

                NewPackageName.ReadOnly = NewPackageSource.ReadOnly = true;
            }
            else
            {
                // This block corresponds to PackageSourcesListBox
                MoveUpButton.Enabled = selectedSource != null && PackageSourcesListBox.SelectedIndex > 0;
                MoveDownButton.Enabled = selectedSource != null && PackageSourcesListBox.SelectedIndex < PackageSourcesListBox.Items.Count - 1;

                bool allowEditing = selectedSource != null;

                BrowseButton.Enabled = updateButton.Enabled = removeButton.Enabled = allowEditing;
                NewPackageName.ReadOnly = NewPackageSource.ReadOnly = !allowEditing;

                // Always enable addButton for PackageSourceListBox
                addButton.Enabled = true;
            }
        }

        private void MoveSelectedItem(int offset)
        {
            if (PackageSourcesListBox.SelectedItem == null)
            {
                return;
            }

            int oldIndex = PackageSourcesListBox.SelectedIndex;
            int newIndex = oldIndex + offset;

            if (newIndex < 0
                || newIndex > PackageSourcesListBox.Items.Count - 1)
            {
                return;
            }
            var item = PackageSourcesListBox.SelectedItem;
            _packageSources.Remove(item);
            _packageSources.Insert(newIndex, item);

            PackageSourcesListBox.SelectedIndex = newIndex;
            UpdateUI();
        }

        internal void InitializeOnActivated()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            // get packages sources
            var allPackageSources = _packageSourceProvider.LoadPackageSources().ToList();
            var packageSources = allPackageSources.Where(ps => !ps.IsMachineWide).ToList();
            var machineWidePackageSources = allPackageSources.Where(ps => ps.IsMachineWide).ToList();
            //_activeSource = _packageSourceProvider.ActivePackageSource;

            // bind to the package sources, excluding Aggregate
            _packageSources = new BindingSource(packageSources.Select(ps => ps.Clone()).ToList(), null);
            _packageSources.CurrentChanged += OnSelectedPackageSourceChanged;
            PackageSourcesListBox.GotFocus += PackageSourcesListBox_GotFocus;
            PackageSourcesListBox.DataSource = _packageSources;

            if (machineWidePackageSources.Count > 0)
            {
                _machineWidepackageSources = new BindingSource(machineWidePackageSources.Select(ps => ps.Clone()).ToList(), null);
                _machineWidepackageSources.CurrentChanged += OnSelectedMachineWidePackageSourceChanged;
                MachineWidePackageSourcesListBox.GotFocus += MachineWidePackageSourcesListBox_GotFocus;
                MachineWidePackageSourcesListBox.DataSource = _machineWidepackageSources;
            }
            else
            {
                MachineWidePackageSourcesListBox.Visible = MachineWideSourcesLabel.Visible = false;
            }

            OnSelectedPackageSourceChanged(null, EventArgs.Empty);
        }

        private void MachineWidePackageSourcesListBox_GotFocus(object sender, EventArgs e)
        {
            if (MachineWidePackageSourcesListBox.SelectedItem == null)
            {
                MachineWidePackageSourcesListBox.SelectedItem = _machineWidepackageSources.Current;
            }
            OnSelectedMachineWidePackageSourceChanged(sender, null);
        }

        private void PackageSourcesListBox_GotFocus(object sender, EventArgs e)
        {
            if (PackageSourcesListBox.SelectedItem == null &&
                _packageSources != null)
            {
                PackageSourcesListBox.SelectedItem = _packageSources.Current;
            }
            OnSelectedPackageSourceChanged(sender, null);
        }

        /// <summary>
        /// Persist the package sources, which was add/removed via the Options page, to the VS Settings store.
        /// This gets called when users click OK button.
        /// </summary>
        internal bool ApplyChangedSettings()
        {
            // if user presses Enter after filling in Name/Source but doesn't click Update
            // the options will be closed without adding the source, try adding before closing
            // Only apply if nothing was added
            TryUpdateSourceResults result = TryUpdateSource();
            if (result != TryUpdateSourceResults.NotUpdated
                &&
                result != TryUpdateSourceResults.Unchanged)
            {
                return false;
            }

            // get package sources as ordered list
            var packageSources = PackageSourcesListBox.Items.Cast<Configuration.PackageSource>().ToList();
            packageSources.AddRange(MachineWidePackageSourcesListBox.Items.Cast<Configuration.PackageSource>().ToList());

            try
            {
                var existingSources = _packageSourceProvider.LoadPackageSources().ToList();
                if (SourcesChanged(existingSources, packageSources))
                {
                    _packageSourceProvider.SavePackageSources(packageSources);
                }
            }
            catch (Configuration.NuGetConfigurationException e)
            {
                MessageHelper.ShowErrorMessage(ExceptionUtilities.DisplayMessage(e), Resources.ErrorDialogBoxTitle);
                return false;
            }

            // find the enabled package source
            return true;
        }

        // Returns true if there are no changes between existingSources and packageSources.
        private static bool SourcesChanged(List<Configuration.PackageSource> existingSources, List<Configuration.PackageSource> packageSources)
        {
            if (existingSources.Count != packageSources.Count)
            {
                return true;
            }

            for (int i = 0; i < existingSources.Count; ++i)
            {
                if (!existingSources[i].Equals(packageSources[i])
                    ||
                    existingSources[i].IsEnabled != packageSources[i].IsEnabled)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// This gets called when users close the Options dialog
        /// </summary>
        internal void ClearSettings()
        {
            // clear this flag so that we will set up the bindings again when the option page is activated next time
            _initialized = false;

            _packageSources = null;
            ClearNameSource();
            UpdateUI();
        }

        private void OnRemoveButtonClick(object sender, EventArgs e)
        {
            if (PackageSourcesListBox.SelectedItem == null)
            {
                return;
            }
            _packageSources.Remove(PackageSourcesListBox.SelectedItem);
            UpdateUI();
        }

        private void OnAddButtonClick(object sender, EventArgs e)
        {
            _packageSources.Add(CreateNewPackageSource());

            // auto-select the newly-added item
            PackageSourcesListBox.SelectedIndex = PackageSourcesListBox.Items.Count - 1;
        }

        private Configuration.PackageSource CreateNewPackageSource()
        {
            var sourcesList = (IEnumerable<Configuration.PackageSource>)_packageSources.List;
            for (int i = 0; ; i++)
            {
                var newName = i == 0 ? "Package source" : "Package source " + i;
                var newSource = i == 0 ? "http://packagesource" : "http://packagesource" + i;
                var packageSource = new Configuration.PackageSource(newSource, newName);
                if (sourcesList.All(ps => !ps.Equals(packageSource)))
                {
                    return packageSource;
                }
            }
        }

        private void OnUpdateButtonClick(object sender, EventArgs e)
        {
            TryUpdateSourceResults result = TryUpdateSource();
            if (result == TryUpdateSourceResults.NotUpdated)
            {
                MessageHelper.ShowWarningMessage(Resources.ShowWarning_NameAndSourceRequired, Resources.ShowWarning_Title);
                SelectAndFocus(NewPackageName);
            }
        }

        private TryUpdateSourceResults TryUpdateSource()
        {
            var name = NewPackageName.Text.Trim();
            var source = NewPackageSource.Text.Trim();
            if (String.IsNullOrWhiteSpace(name)
                && String.IsNullOrWhiteSpace(source))
            {
                return TryUpdateSourceResults.NotUpdated;
            }

            // validate name
            if (String.IsNullOrWhiteSpace(name))
            {
                MessageHelper.ShowWarningMessage(Resources.ShowWarning_NameRequired, Resources.ShowWarning_Title);
                SelectAndFocus(NewPackageName);
                return TryUpdateSourceResults.InvalidSource;
            }

            // validate source
            if (String.IsNullOrWhiteSpace(source))
            {
                MessageHelper.ShowWarningMessage(Resources.ShowWarning_SourceRequried, Resources.ShowWarning_Title);
                SelectAndFocus(NewPackageSource);
                return TryUpdateSourceResults.InvalidSource;
            }

            if (!(Common.PathValidator.IsValidLocalPath(source) || Common.PathValidator.IsValidUncPath(source) || Common.PathValidator.IsValidUrl(source)))
            {
                MessageHelper.ShowWarningMessage(Resources.ShowWarning_InvalidSource, Resources.ShowWarning_Title);
                SelectAndFocus(NewPackageSource);
                return TryUpdateSourceResults.InvalidSource;
            }

            var selectedPackageSource = (Configuration.PackageSource)PackageSourcesListBox.SelectedItem;
            if (selectedPackageSource == null)
            {
                return TryUpdateSourceResults.NotUpdated;
            }

            var newPackageSource = new Configuration.PackageSource(source, name, selectedPackageSource.IsEnabled);
            if (selectedPackageSource.Equals(newPackageSource))
            {
                return TryUpdateSourceResults.Unchanged;
            }

            var sourcesList = (IEnumerable<Configuration.PackageSource>)_packageSources.List;

            // check to see if name has already been added
            // also make sure it's not the same as the aggregate source ('All')
            bool hasName = sourcesList.Any(ps => ps != selectedPackageSource &&
                                                 String.Equals(name, ps.Name, StringComparison.CurrentCultureIgnoreCase));
            if (hasName)
            {
                MessageHelper.ShowWarningMessage(Resources.ShowWarning_UniqueName, Resources.ShowWarning_Title);
                SelectAndFocus(NewPackageName);
                return TryUpdateSourceResults.SourceConflicted;
            }

            // check to see if source has already been added
            bool hasSource = sourcesList.Any(ps => ps != selectedPackageSource &&
                                                   String.Equals(PackageManagement.VisualStudio.PathValidator.GetCanonicalPath(source),
                                                                 PackageManagement.VisualStudio.PathValidator.GetCanonicalPath(ps.Source),
                                                                 StringComparison.OrdinalIgnoreCase));
            if (hasSource)
            {
                MessageHelper.ShowWarningMessage(Resources.ShowWarning_UniqueSource, Resources.ShowWarning_Title);
                SelectAndFocus(NewPackageSource);
                return TryUpdateSourceResults.SourceConflicted;
            }

            _packageSources[_packageSources.Position] = newPackageSource;

            return TryUpdateSourceResults.Successful;
        }

        private static void SelectAndFocus(TextBox textBox)
        {
            textBox.Focus();
            textBox.SelectAll();
        }

        private void ClearNameSource()
        {
            NewPackageName.Text = String.Empty;
            NewPackageSource.Text = String.Empty;
            NewPackageName.Focus();
        }

        private void PackageSourcesContextMenu_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            var currentListBox = PackageSourcesContextMenu.SourceControl as ListBox;
            if (currentListBox != null
                && currentListBox.SelectedItem != null
                && e.ClickedItem == CopyPackageSourceStripMenuItem)
            {
                CopySelectedItem((Configuration.PackageSource)currentListBox.SelectedItem);
            }
        }

        private void PackageSourcesListBox_KeyUp(object sender, KeyEventArgs e)
        {
            var currentListBox = (ListBox)sender;
            if (e.KeyCode == Keys.C
                && e.Control)
            {
                CopySelectedItem((Configuration.PackageSource)currentListBox.SelectedItem);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Space)
            {
                TogglePackageSourceEnabled(currentListBox.SelectedIndex, currentListBox);
                e.Handled = true;
            }
        }

        private void TogglePackageSourceEnabled(int itemIndex, ListBox currentListBox)
        {
            if (itemIndex < 0
                || itemIndex >= currentListBox.Items.Count)
            {
                return;
            }

            var item = (Configuration.PackageSource)currentListBox.Items[itemIndex];
            item.IsEnabled = !item.IsEnabled;

            currentListBox.Invalidate(GetCheckBoxRectangleForListBoxItem(currentListBox, itemIndex));
        }

        private Rectangle GetCheckBoxRectangleForListBoxItem(ListBox currentListBox, int itemIndex)
        {
            const int edgeMargin = 8;

            Rectangle itemRectangle = currentListBox.GetItemRectangle(itemIndex);

            // this is the bound of the checkbox
            var checkBoxRectangle = new Rectangle(
                itemRectangle.Left + edgeMargin + 2,
                itemRectangle.Top + edgeMargin,
                _checkBoxSize.Width,
                _checkBoxSize.Height);

            return checkBoxRectangle;
        }

        private static void CopySelectedItem(Configuration.PackageSource selectedPackageSource)
        {
            Clipboard.Clear();
            Clipboard.SetText(selectedPackageSource.Source);
        }

        private void PackageSourcesListBox_MouseUp(object sender, MouseEventArgs e)
        {
            var currentListBox = (ListBox)sender;
            if (e.Button == MouseButtons.Right)
            {
                int itemIndexToSelect = currentListBox.IndexFromPoint(e.Location);
                if (itemIndexToSelect >= 0 && itemIndexToSelect < currentListBox.Items.Count)
                {
                    currentListBox.SelectedIndex = itemIndexToSelect;
                }
            }
            else if (e.Button == MouseButtons.Left)
            {
                int itemIndex = currentListBox.SelectedIndex;
                if (itemIndex >= 0
                    && itemIndex < currentListBox.Items.Count)
                {
                    Rectangle checkBoxRectangle = GetCheckBoxRectangleForListBoxItem(currentListBox, itemIndex);
                    // if the mouse click position is inside the checkbox, toggle the IsEnabled property
                    if (checkBoxRectangle.Contains(e.Location))
                    {
                        TogglePackageSourceEnabled(itemIndex, currentListBox);
                    }
                }
            }
        }

        private void PackageSourcesListBox_DrawItem(object sender, DrawItemEventArgs e)
        {
            var currentListBox = (ListBox)sender;
            Graphics graphics = e.Graphics;
            e.DrawBackground();

            if (e.Index < 0
                || e.Index >= currentListBox.Items.Count)
            {
                return;
            }

            var currentItem = (Configuration.PackageSource)currentListBox.Items[e.Index];

            using (StringFormat drawFormat = new StringFormat())
            {
                using (Brush foreBrush = new SolidBrush(currentListBox.SelectionMode == SelectionMode.None ? SystemColors.WindowText : e.ForeColor))
                {
                    drawFormat.Alignment = StringAlignment.Near;
                    drawFormat.Trimming = StringTrimming.EllipsisCharacter;
                    drawFormat.LineAlignment = StringAlignment.Near;
                    drawFormat.FormatFlags = StringFormatFlags.NoWrap;

                    // the margin between the checkbox and the edge of the list box
                    const int edgeMargin = 8;
                    // the margin between the checkbox and the text
                    const int textMargin = 4;

                    // draw the enabled/disabled checkbox
                    CheckBoxState checkBoxState = currentItem.IsEnabled ? CheckBoxState.CheckedNormal : CheckBoxState.UncheckedNormal;
                    Size checkBoxSize = CheckBoxRenderer.GetGlyphSize(graphics, checkBoxState);
                    CheckBoxRenderer.DrawCheckBox(
                        graphics,
                        new Point(edgeMargin, e.Bounds.Top + edgeMargin),
                        checkBoxState);

                    if (_checkBoxSize.IsEmpty)
                    {
                        // save the checkbox size so that we can detect mouse click on the
                        // checkbox in the MouseUp event handler.
                        // here we assume that all checkboxes have the same size, which is reasonable.
                        _checkBoxSize = checkBoxSize;
                    }

                    GraphicsState oldState = graphics.Save();
                    try
                    {
                        // turn on high quality text rendering mode
                        graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                        // draw each package source as
                        //
                        // [checkbox] Name
                        //            Source (italics)

                        int textWidth = e.Bounds.Width - checkBoxSize.Width - edgeMargin - textMargin;

                        SizeF nameSize = graphics.MeasureString(currentItem.Name, e.Font, textWidth, drawFormat);

                        // resize the bound rectangle to make room for the checkbox above
                        var nameBounds = new Rectangle(
                            e.Bounds.Left + checkBoxSize.Width + edgeMargin + textMargin,
                            e.Bounds.Top,
                            textWidth,
                            (int)nameSize.Height);

                        graphics.DrawString(currentItem.Name, e.Font, foreBrush, nameBounds, drawFormat);

                        var sourceBounds = new Rectangle(
                            nameBounds.Left,
                            nameBounds.Bottom,
                            textWidth,
                            e.Bounds.Bottom - nameBounds.Bottom);
                        graphics.DrawString(currentItem.Source, e.Font, foreBrush, sourceBounds, drawFormat);
                    }
                    finally
                    {
                        graphics.Restore(oldState);
                    }

                    // If the ListBox has focus, draw a focus rectangle around the selected item.
                    e.DrawFocusRectangle();
                }
            }
        }

        private void PackageSourcesListBox_MeasureItem(object sender, MeasureItemEventArgs e)
        {
            var currentListBox = (ListBox)sender;
            if (e.Index < 0
                || e.Index >= currentListBox.Items.Count)
            {
                return;
            }

            var currentItem = (Configuration.PackageSource)currentListBox.Items[e.Index];
            using (StringFormat drawFormat = new StringFormat())
            {
                using (Font italicFont = new Font(Font, FontStyle.Italic))
                {
                    drawFormat.Alignment = StringAlignment.Near;
                    drawFormat.Trimming = StringTrimming.EllipsisCharacter;
                    drawFormat.LineAlignment = StringAlignment.Near;
                    drawFormat.FormatFlags = StringFormatFlags.NoWrap;

                    SizeF nameLineHeight = e.Graphics.MeasureString(currentItem.Name, Font, e.ItemWidth, drawFormat);
                    SizeF sourceLineHeight = e.Graphics.MeasureString(currentItem.Source, italicFont, e.ItemWidth, drawFormat);

                    e.ItemHeight = (int)Math.Ceiling(nameLineHeight.Height + sourceLineHeight.Height);
                }
            }
        }

        private void PackageSourcesListBox_MouseMove(object sender, MouseEventArgs e)
        {
            var currentListBox = (ListBox)sender;
            int index = currentListBox.IndexFromPoint(e.X, e.Y);

            if (index >= 0
                && index < currentListBox.Items.Count
                && e.Y <= currentListBox.PreferredHeight)
            {
                var source = (Configuration.PackageSource)currentListBox.Items[index];
                string newToolTip = !String.IsNullOrEmpty(source.Description) ?
                    source.Description :
                    source.Source;
                string currentToolTip = packageListToolTip.GetToolTip(currentListBox);
                if (currentToolTip != newToolTip)
                {
                    packageListToolTip.SetToolTip(currentListBox, newToolTip);
                }
            }
            else
            {
                packageListToolTip.SetToolTip(currentListBox, null);
                packageListToolTip.Hide(currentListBox);
            }
        }

        private void OnSelectedPackageSourceChanged(object sender, EventArgs e)
        {
            MachineWidePackageSourcesListBox.ClearSelected();
            UpdateUI();

            UpdateTextBoxes((Configuration.PackageSource)_packageSources.Current);
        }

        private void OnSelectedMachineWidePackageSourceChanged(object sender, EventArgs e)
        {
            PackageSourcesListBox.ClearSelected();
            UpdateUI();

            UpdateTextBoxes((Configuration.PackageSource)_machineWidepackageSources.Current);
        }

        private void UpdateTextBoxes(Configuration.PackageSource packageSource)
        {
            if (packageSource != null)
            {
                NewPackageName.Text = packageSource.Name;
                NewPackageSource.Text = packageSource.Source;
            }
            else
            {
                NewPackageName.Text = String.Empty;
                NewPackageSource.Text = String.Empty;
            }
        }

        [SuppressMessage("Microsoft.VisualStudio.Threading.Analyzers", "VSTHRD010", Justification = "NuGet/Home#4833 Baseline")]
        private void OnBrowseButtonClicked(object sender, EventArgs e)
        {
            const int MaxDirectoryLength = 1000;

            //const int BIF_RETURNONLYFSDIRS = 0x00000001;   // For finding a folder to start document searching.
            const int BIF_BROWSEINCLUDEURLS = 0x00000080; // Allow URLs to be displayed or entered.

            var uiShell = (IVsUIShell2)_serviceProvider.GetService(typeof(SVsUIShell));

            char[] rgch = new char[MaxDirectoryLength + 1];

            // allocate a buffer in unmanaged memory for file name (string)
            IntPtr bufferPtr = Marshal.AllocCoTaskMem((rgch.Length + 1) * 2);
            // copy initial path to bufferPtr
            Marshal.Copy(rgch, 0, bufferPtr, rgch.Length);

            VSBROWSEINFOW[] pBrowse = new VSBROWSEINFOW[1];
            pBrowse[0] = new VSBROWSEINFOW
            {
                lStructSize = (uint)Marshal.SizeOf(pBrowse[0]),
                dwFlags = BIF_BROWSEINCLUDEURLS,
                pwzDlgTitle = Resources.BrowseFolderDialogDescription,
                nMaxDirName = MaxDirectoryLength,
                hwndOwner = Handle,
                pwzDirName = bufferPtr,
                pwzInitialDir = DetermineInitialDirectory()
            };

            var browseInfo = new VSNSEBROWSEINFOW[1] { new VSNSEBROWSEINFOW() };

            int ret = uiShell.GetDirectoryViaBrowseDlgEx(pBrowse, "", Resources.BrowseFolderDialogSelectButton, "", browseInfo);
            if (ret == VSConstants.S_OK)
            {
                var pathPtr = pBrowse[0].pwzDirName;
                var path = Marshal.PtrToStringAuto(pathPtr);
                NewPackageSource.Text = path;

                // if the package name text box is empty, we fill it with the selected folder's name
                if (string.IsNullOrEmpty(NewPackageName.Text))
                {
                    NewPackageName.Text = Path.GetFileName(path);
                }
            }
        }

        private string DetermineInitialDirectory()
        {
            // determine the inital directory to show in the folder dialog
            string initialDir = NewPackageSource.Text;

            if (IsPathRootedSafe(initialDir)
                && Directory.Exists(initialDir))
            {
                return initialDir;
            }

            var selectedItem = (Configuration.PackageSource)PackageSourcesListBox.SelectedItem;
            if (selectedItem != null)
            {
                initialDir = selectedItem.Source;
                if (IsPathRootedSafe(initialDir))
                {
                    return initialDir;
                }
            }

            // fallback to MyDocuments folder
            return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        private static bool IsPathRootedSafe(string path)
        {
            // Check to make sure path does not contain any invalid chars.
            // Otherwise, Path.IsPathRooted() will throw an ArgumentException.
            return path.IndexOfAny(Path.GetInvalidPathChars()) == -1 && Path.IsPathRooted(path);
        }
    }

    internal enum TryUpdateSourceResults
    {
        NotUpdated = 0,
        Successful = 1,
        InvalidSource = 2,
        SourceConflicted = 3,
        Unchanged = 4
    }
}
