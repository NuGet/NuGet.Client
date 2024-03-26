// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.InteropServices;

namespace NuGet.SolutionRestoreManager
{
    [ComImport]
    [Guid("2EBF7561-7658-42D8-814D-A3A40F86C77D")]
    public interface IVsReferenceItemType
    {
        /// <summary>
        /// The item type.
        /// </summary>
        string ItemType { get; }

        /// <summary>
        /// The collection of items of the specified item type.
        /// </summary>
        IVsReferenceItems Items { get; }
    }
}
