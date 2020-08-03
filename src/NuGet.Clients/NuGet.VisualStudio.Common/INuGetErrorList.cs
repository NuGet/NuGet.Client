// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.VisualStudio.Common
{
    /// <summary> Error list abstraction. </summary>
    public interface INuGetErrorList : IDisposable
    {
        /// <summary> Add NuGet entries. </summary>
        /// <param name="entries"> NuGet entries to add to the error list. </param>
        void AddNuGetEntries(params ErrorListTableEntry[] entries);

        /// <summary> Bring to front if settings permit. </summary>
        void BringToFrontIfSettingsPermit();

        /// <summary> Clear NuGet entries. </summary>
        void ClearNuGetEntries();
    }
}
