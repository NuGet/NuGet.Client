// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.PackageManagement.UI
{
    public class PreviewWindowModel
    {
        public IEnumerable<PreviewResult> PreviewResults { get; }

        public string Title { get; private set; }

        public PreviewWindowModel(IEnumerable<PreviewResult> results)
        {
            PreviewResults = results;
            Title = Resources.WindowTitle_Preview;
        }
    }
}
