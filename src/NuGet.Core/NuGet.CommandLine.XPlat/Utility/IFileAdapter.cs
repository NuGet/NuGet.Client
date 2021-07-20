// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.CommandLine.XPlat.Utility
{
    internal interface IFileAdapter
    {
        bool Exists(string path);
    }
}
