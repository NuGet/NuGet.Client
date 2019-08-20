using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Packaging.Signing
{
    internal interface IRfc3161TimestampTokenInfo
    {
#if IS_SIGNING_SUPPORTED
        string PolicyId { get; }

        DateTimeOffset Timestamp { get; }

        long? AccuracyInMicroseconds { get; }

        Oid HashAlgorithmId { get; }

        bool HasMessageHash(byte[] hash);

#endif
    }
}
