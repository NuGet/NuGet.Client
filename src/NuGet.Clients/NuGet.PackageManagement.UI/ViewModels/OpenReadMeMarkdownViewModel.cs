// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Common;

namespace NuGet.PackageManagement.UI
{
    public class OpenReadMeMarkdownViewModel : INotifyPropertyChanged
    {
        private string _fileContent;
        public OpenReadMeMarkdownViewModel(string filePath)
        {
            FileContent = "";
        }

        public string FileContent
        {
            get
            {
                return _fileContent;
            }

            private set
            {
                _fileContent = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FileContent)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public async Task ReadMarkdownFileContentsAsync(string filePath)
        {
            try
            {
                FileContent = await FileUtility.SafeReadAsync(filePath, async (stream, filePath) =>
                {
                    using (var reader = new StreamReader(stream))
                    {
                        return await reader.ReadToEndAsync();
                    }
                });
            }
            catch (FileNotFoundException)
            {

            }
        }
    }
}
