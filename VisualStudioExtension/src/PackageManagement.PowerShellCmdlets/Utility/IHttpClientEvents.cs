// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    public interface IHttpClientEvents : IProgressProvider
    {
        event EventHandler<WebRequestEventArgs> SendingRequest;
    }

    public interface IProgressProvider
    {
        event EventHandler<ProgressEventArgs> ProgressAvailable;
    }

    public class ProgressEventArgs : EventArgs
    {
        public ProgressEventArgs(int percentComplete)
            : this(null, percentComplete)
        {
        }

        public ProgressEventArgs(string operation, int percentComplete)
        {
            Operation = operation;
            PercentComplete = percentComplete;
        }

        public string Operation { get; private set; }
        public int PercentComplete { get; private set; }
    }
}
