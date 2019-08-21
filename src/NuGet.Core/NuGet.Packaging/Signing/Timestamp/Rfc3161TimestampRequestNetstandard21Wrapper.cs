using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Packaging.Signing
{
#if IS_SIGNING_SUPPORTED && NETSTANDARD2_1
    public class Rfc3161TimestampRequestNetstandard21Wrapper : IRfc3161TimestampRequest
    {
        private System.Security.Cryptography.Pkcs.Rfc3161TimestampRequest _rfc3161TimestampRequest;
        public Rfc3161TimestampRequestNetstandard21Wrapper(
                byte[] messageHash,
                HashAlgorithmName hashAlgorithm,
                Oid requestedPolicyId = null,
                byte[] nonce = null,
                bool requestSignerCertificates = false,
                X509ExtensionCollection extensions = null)
        {
            _rfc3161TimestampRequest = System.Security.Cryptography.Pkcs.Rfc3161TimestampRequest.CreateFromHash(
                new ReadOnlySpan<byte>(messageHash),
                hashAlgorithm,
                requestedPolicyId = null,
                nonce = null,
                requestSignerCertificates = false,
                extensions = null);
        }

        public unsafe IRfc3161TimestampToken SubmitRequest(Uri timestampUri, TimeSpan timeout)
        {
            return _rfc3161TimestampRequest.SubmitRequest(timestampUri, timeout);
        }
    }
#endif
}
