﻿using NuGetClient.Test.Foundation.TestAttributes;
using NuGetClient.Test.Foundation.TestAttributes.Context;

namespace NuGetClient.Test.Foundation
{
    /// <summary>
    /// Base test class. Provide a central place to add harness functionality
    /// </summary>
    [TrackingTestClass]
    public class TestClass
    {
        /// <summary>
        /// This base class exists to allow us to add harness related functionality should we need to. It should not contain actual test code.
        /// (No setup/teardown of product code)
        /// </summary>
        public virtual Context CurrentContext { get; set; }
    }
}
