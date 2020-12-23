// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Drawing;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using NuGet.PackageManagement.UI;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.Options
{
    internal class PackageSourceCheckedListBox : CheckedListBox
    {
        public Size CheckBoxSize { get; set; }

        public override int ItemHeight
        {
            get
            {
                var g = CreateGraphics();
                using (var drawFormat = new StringFormat())
                {
                    using (var italicFont = new Font(Font, FontStyle.Italic))
                    {
                        var nameLineHeight = g.MeasureString("SampleText", Font);
                        var sourceLineHeight = g.MeasureString("SampleText", italicFont);

                        return (int)Math.Ceiling(nameLineHeight.Height + sourceLineHeight.Height);
                    }
                }
            }
            set { base.ItemHeight = value; }
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            var currentListBox = this;
            var graphics = e.Graphics;
            e.DrawBackground();

            if (e.Index < 0 || e.Index >= currentListBox.Items.Count)
            {
                return;
            }

            var currentItem = (PackageSourceContextInfo)currentListBox.Items[e.Index];

            using (var drawFormat = new StringFormat())
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
                    var checkBoxState = currentItem.IsEnabled ? CheckBoxState.CheckedNormal : CheckBoxState.UncheckedNormal;
                    var checkBoxSize = CheckBoxRenderer.GetGlyphSize(graphics, checkBoxState);
                    CheckBoxRenderer.DrawCheckBox(
                        graphics,
                        new Point(edgeMargin, e.Bounds.Top + edgeMargin),
                        checkBoxState);

                    if (CheckBoxSize.IsEmpty)
                    {
                        // save the checkbox size so that we can detect mouse click on the
                        // checkbox in the MouseUp event handler.
                        // here we assume that all checkboxes have the same size, which is reasonable.
                        CheckBoxSize = checkBoxSize;
                    }

                    var oldState = graphics.Save();
                    try
                    {
                        // turn on high quality text rendering mode
                        graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                        // draw each package source as
                        //
                        // [checkbox] Name
                        //            Source (italics)

                        var textWidth = e.Bounds.Width - checkBoxSize.Width - edgeMargin - textMargin;

                        var nameSize = graphics.MeasureString(currentItem.Name, e.Font, textWidth, drawFormat);

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

        protected override AccessibleObject CreateAccessibilityInstance()
        {
            return new CheckedListBoxAccessibleObject(this);
        }

        internal int FocusedIndex
        {
            get
            {
                if (IsHandleCreated)
                {
                    return unchecked((int)(long)SendMessage(NativeMethods.LB_GETCARETINDEX, 0, 0));
                }

                return -1;
            }
        }

        internal IntPtr SendMessage(int msg, int wparam, int lparam)
        {
            return NativeMethods.SendMessage(new HandleRef(this, Handle), msg, wparam, lparam);
        }
    }
}
