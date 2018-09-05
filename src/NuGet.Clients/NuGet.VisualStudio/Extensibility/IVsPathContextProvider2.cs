// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.InteropServices;

namespace NuGet.VisualStudio
{
    /// <summary>
    /// A factory to initialize <see cref="IVsPathContext2"/> instances.
    /// </summary>
    [ComImport]
    [Guid("5BAC7095-F674-4778-8788-E15FFF77F96B")]
    public interface IVsPathContextProvider2 : IVsPathContextProvider
    {
        /// <summary>
        /// Attempts to create an instance of <see cref="IVsPathContext2"/> for the solution.
        /// </summary>
        /// <param name="context">The path context associated with this solution.</param>
        /// <returns>
        /// <code>True</code> if operation has succeeded and context was created.
        /// <code>False</code> otherwise.
        /// </returns>
        /// <throws>
        /// <code>InvalidOperationException</code> when it fails to create a context and return appropriate error message.
        /// </throws>
        bool TryCreateSolutionContext(out IVsPathContext2 context);

        /// <summary>
        /// Attempts to create an instance of <see cref="IVsPathContext2"/> for the solution.
        /// </summary>
        /// <param name="solutionDirectory">
        /// path to the solution directory. Must be an absolute path.
        /// It will be performant to pass the solution directory if it's available.
        /// </param>
        /// <param name="context">The path context associated with this solution.</param>
        /// <returns>
        /// <code>True</code> if operation has succeeded and context was created.
        /// <code>False</code> otherwise.
        /// </returns>
        /// <throws>
        /// <code>ArgumentNullException</code> if solutionDirectory is passed as null.
        /// <code>InvalidOperationException</code> when it fails to create a context and return appropriate error message.
        /// </throws>
        bool TryCreateSolutionContext(string solutionDirectory, out IVsPathContext2 context);
    }
}
