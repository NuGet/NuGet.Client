// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.VisualStudio.Markdown.Platform;

namespace NuGet.PackageManagement.UI.ViewModels
{
    public sealed class ReadMePreviewViewModel : ViewModelBase, IDisposable
    {
#pragma warning disable CS0618 // Type or member is obsolete
        private IMarkdownPreview _markdownPreview;
#pragma warning restore CS0618 // Type or member is obsolete

        public ReadMePreviewViewModel()
        {
#pragma warning disable CS0618 // Type or member is obsolete
            _markdownPreview = new PreviewBuilder().Build();
#pragma warning restore CS0618 // Type or member is obsolete
            _ = UpdateMarkdownAsync("");
            MarkdownPreviewControl = _markdownPreview.VisualElement;
        }

        private FrameworkElement _markdownPreviewControl;
        /// <summary>
        /// The markdown preview control if it's available
        /// </summary>
        public FrameworkElement MarkdownPreviewControl
        {
            get => _markdownPreviewControl;
            private set
            {
                _markdownPreviewControl = value;
                RaisePropertyChanged(nameof(MarkdownPreviewControl));
            }
        }

        private bool _isErrorWithReadMe;

        public bool IsErrorWithReadMe
        {
            get => _isErrorWithReadMe;
            set
            {
                _isErrorWithReadMe = value;
                RaisePropertyChanged(nameof(IsErrorWithReadMe));
            }
        }

        public async Task UpdateMarkdownAsync(string markDown)
        {
            try
            {
                IsErrorWithReadMe = false;
                _markdownPreview.VisualElement.Visibility = Visibility.Collapsed;
                await _markdownPreview.UpdateContentAsync("", ScrollHint.None);
                if (!string.IsNullOrWhiteSpace(markDown))
                {
                    await _markdownPreview.UpdateContentAsync(markDown, ScrollHint.None);
                    _markdownPreview.VisualElement.Visibility = Visibility.Visible;
                }
            }
            catch (Exception)
            {
                IsErrorWithReadMe = true;
                //ErrorMessage = Resources.Error_ProjectNotInCache ex.Message;
                //need to log ex somewhere?
            }
        }

        public void Dispose()
        {
            _markdownPreview?.Dispose();
            _markdownPreview = null;
        }
    }
}
