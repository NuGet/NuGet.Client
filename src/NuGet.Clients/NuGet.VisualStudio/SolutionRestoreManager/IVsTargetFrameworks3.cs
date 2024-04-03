// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections;

namespace NuGet.SolutionRestoreManager
{
#pragma warning disable CA1010 // Collections should implement generic interface
    public interface IVsTargetFrameworks3 : IEnumerable
#pragma warning restore CA1010 // Collections should implement generic interface
    {
        /// <summary>
        /// Total count of references in container.
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Retrieves a reference by name or index.
        /// </summary>
        /// <param name="index">Reference name or index.</param>
        /// <returns>Reference item matching index.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="index" /> is <see langword="null" />.</exception>
        IVsTargetFrameworkInfo4 Item(object index);
    }
}
