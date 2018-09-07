// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.VisualStudio.Facade
{
    /// <summary>
    /// The data available when the <see cref="IRestoreEvents.SolutionRestoreCompleted"/> event is raised.
    /// </summary>
    public class SolutionRestoredEventArgs : EventArgs
    {
        public SolutionRestoredEventArgs(bool isSuccess, string solutionSpecHash)
        {
            if (string.IsNullOrEmpty(solutionSpecHash))
            {
                throw new ArgumentNullException(nameof(solutionSpecHash));
            }

            IsSuccess = isSuccess;
            SolutionSpecHash = solutionSpecHash;
        }

        public bool IsSuccess { get; }

        public string SolutionSpecHash { get; }
    }
}
