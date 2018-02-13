// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Configuration
{
    public interface ITrustEntry
    {
        /// <summary>
        /// The priority of this entry in the nuget.config hierarchy. Same as SettingValue.Priority.
        /// Should be used only if this entry is read from a config file.
        /// </summary>
        int Priority { get; }
    }
}
