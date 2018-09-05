// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace NuGet.Test.Utility
{
    /// <summary>
    /// This class discovers all of the tests and test classes that have
    /// applied the Platform trait attribute
    /// </summary>
    public class PlatformDiscoverer : ITraitDiscoverer
    {
        /// <summary>
        /// Gets the trait values from the Platform attribute.
        /// </summary>
        /// <param name="traitAttribute">The trait attribute containing the trait values.</param>
        /// <returns>The trait values.</returns>
        public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
        {
            var ctorArgs = traitAttribute.GetConstructorArguments().ToList();
            yield return new KeyValuePair<string, string>("Platform", ctorArgs[0].ToString());
        }
    }
}