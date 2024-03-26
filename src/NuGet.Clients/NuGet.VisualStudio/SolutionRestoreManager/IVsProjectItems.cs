// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections;
using System.Runtime.InteropServices;

namespace NuGet.SolutionRestoreManager
{
    [ComImport]
    [Guid("2CD137C1-4B0B-4775-8F67-C8D8D69542A2")]
    public interface IVsProjectItems : IEnumerable
    {
        /// <summary>
        /// Total count of project item types
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Retrieves a reference item type by name or index.
        /// </summary>
        /// <param name="index">Item type name or index.</param>
        /// <returns>Reference item type</returns>
        IVsReferenceItemType Item(object index);
    }
}
