using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Packaging.Signing
{
    public class IRfc3161TimestampRequestFactory
    {
        public static IRfc3161TimestampRequest CreateIRfc3161TimestampRequest(
            byte[] messageHash,
            HashAlgorithmName hashAlgorithm,
            Oid requestedPolicyId,
            byte[] nonce,
            bool requestSignerCertificates,
            X509ExtensionCollection extensions)
        {
            IRfc3161TimestampRequest iRfc3161TimestampRequest = null;
#if IS_DESKTOP
            iRfc3161TimestampRequest = new Rfc3161TimestampRequestNet472Wrapper(
                messageHash,
                hashAlgorithm,
                requestedPolicyId,
                nonce,
                requestSignerCertificates,
                extensions);
#endif

#if NETSTANDARD2_1
            iRfc3161TimestampRequest = new Rfc3161TimestampRequestNetstandard21Wrapper(
                messageHash,
                hashAlgorithm,
                requestedPolicyId,
                nonce,
                requestSignerCertificates,
                extensions);
#endif
            return iRfc3161TimestampRequest;
        }

    }
}
