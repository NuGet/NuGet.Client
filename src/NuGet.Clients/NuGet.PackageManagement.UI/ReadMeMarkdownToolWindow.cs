// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;

namespace NuGet.PackageManagement.UI
{
    [Guid(Constants.ReadMeMarkdownToolWindowGuid)]
    public class ReadMeMarkdownToolWindow : ToolWindowPane
    {
        private OpenReadMeMarkdownViewModel _viewModel;

        public ReadMeMarkdownToolWindow() : base(null)
        {
            _viewModel = new OpenReadMeMarkdownViewModel(string.Empty);

            Content = new OpenReadMeMarkdownView(_viewModel);
        }

        //public string FilePath { get; set;}

        public Task UpdateAsync(string filePath)
        {
            Caption = filePath;
            return _viewModel.ReadMarkdownFileContentsAsync(filePath);
        }
        /*
        public void SetContent(string content)
        {
            Content = new OpenReadMeMarkdownView(new OpenReadMeMarkdownViewModel(content));
        }
        */
    }
}
