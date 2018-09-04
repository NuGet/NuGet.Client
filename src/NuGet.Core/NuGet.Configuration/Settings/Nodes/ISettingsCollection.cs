// Copyright(c) .NET Foundation.All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Configuration
{
    internal interface ISettingsCollection : ISettingsNode
    {
        string Name { get; }

        bool AddChild(SettingsNode child, bool isBatchOperation = false);

        bool RemoveChild(SettingsNode child, bool isBatchOperation = false);
    }
}

