// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Configuration
{
    public interface ITrustEntry
    {
        /// <summary>
        /// The priority of this entry in the nuget.config hierarchy. Same as SettingValue.Priority.
        /// Null if this entry is not read from a config file.
        /// </summary>
        int? Priority { get; }
    }
}
