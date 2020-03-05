// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Test.Utility.Signing
{
    /// <summary>
    /// Package names ensure uniqueness in path when signed packages are stored on disk for verification in later steps
    /// </summary>
    public enum TestPackages
    {
        /// <summary>
        /// This package is author signed with a timestamp.
        /// The timestamp signature does not include the signing certificate.
        /// Certificates are otherwise trusted and valid.
        /// </summary>
        Package1
    }
}
