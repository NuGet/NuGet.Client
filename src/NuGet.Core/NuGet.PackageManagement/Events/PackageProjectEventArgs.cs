// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.PackageManagement
{
    /// <summary>
    /// Event arguments for nuget batch events.
    /// </summary>
    public class PackageProjectEventArgs : EventArgs
    {
        public PackageProjectEventArgs(string id, string name)
        {
            Id = id;
            Name = name;
        }

        public string Id { get; }
        public string Name { get; }
    }
}
