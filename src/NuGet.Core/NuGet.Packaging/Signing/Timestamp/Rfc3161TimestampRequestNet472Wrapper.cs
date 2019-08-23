using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Packaging.Signing
{
#if IS_SIGNING_SUPPORTED && IS_DESKTOP
    public class Rfc3161TimestampRequestNet472Wrapper : IRfc3161TimestampRequest
    {
        private NuGet.Packaging.Signing.Rfc3161TimestampRequest _rfc3161TimestampRequest;

        public Rfc3161TimestampRequestNet472Wrapper(
            byte[] messageHash,
            HashAlgorithmName hashAlgorithm,
            Oid requestedPolicyId,
            byte[] nonce,
            bool requestSignerCertificates,
            X509ExtensionCollection extensions)
        {
            _rfc3161TimestampRequest = new Rfc3161TimestampRequest(
                messageHash,
                hashAlgorithm,
                requestedPolicyId,
                nonce,
                requestSignerCertificates,
                extensions);
        }

        public Task<IRfc3161TimestampToken> SubmitRequestAsync(Uri timestampUri, TimeSpan timeout)
        {
            return Task.FromResult(_rfc3161TimestampRequest.SubmitRequest(timestampUri, timeout));
        }
    }
#endif
}
