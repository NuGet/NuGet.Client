// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.PackageManagement.UI;
using NuGet.ProjectManagement;

namespace StandaloneUI
{
    internal class StandaloneDialogProgress : IProgress<ProgressDialogData>
    {
        private readonly string _caption;
        private readonly INuGetProjectContext _logger;

        public StandaloneDialogProgress(string caption, INuGetProjectContext logger)
        {
            _caption = caption;
            _logger = logger;
        }

        public void Report(ProgressDialogData value)
        {
            _logger.Log(MessageLevel.Info, $"Progress dialog '{_caption}': {value.ProgressText}: {value.WaitMessage}");
        }
    }
}