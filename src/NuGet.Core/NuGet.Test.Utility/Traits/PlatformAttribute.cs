// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Xunit.Sdk;

namespace NuGet.Test.Utility
{
    /// <summary>
    /// Test trait attribute applied to a test method to specify a platform filter.
    /// </summary>
    [TraitDiscoverer("NuGet.Test.Utility.PlatformDiscoverer", "NuGet.Test.Utility")]
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class PlatformAttribute : Attribute, ITraitAttribute
    {
        public PlatformAttribute(string platform) { }
    }
}