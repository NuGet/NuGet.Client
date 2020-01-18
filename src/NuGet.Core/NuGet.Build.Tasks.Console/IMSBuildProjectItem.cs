// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Commands;

namespace NuGet.Build.Tasks.Console
{
    /// <summary>
    /// Represents an <see cref="IMSBuildItem"/> that allows properties updates.
    /// </summary>
    internal interface IMSBuildProjectItem : IMSBuildItem
    {
        /// <summary>
        /// Adds the specific property to the <see cref="IMSBuildItem.Properties"> or updates its value if the property is already present.</see>.
        /// </summary>
        /// <param name="name">The property name.</param>
        /// <param name="value">The property value.</param>
        void AddOrUpdateProperties(string name, string value);
    }
}
