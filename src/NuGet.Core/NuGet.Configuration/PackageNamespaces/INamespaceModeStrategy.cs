// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Configuration
{
    /// <summary>
    /// Interface for various namespace modes.
    /// </summary>
    internal interface INamespaceModeStrategy
    {
        ValidationResult ValidateRule(string packageId, IReadOnlyList<string> sources);
    }
}
