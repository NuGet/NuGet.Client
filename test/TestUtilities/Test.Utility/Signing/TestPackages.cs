using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
