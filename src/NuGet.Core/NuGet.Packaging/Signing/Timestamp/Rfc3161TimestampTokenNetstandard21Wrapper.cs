using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Packaging.Signing
{
#if IS_SIGNING_SUPPORTED && NETSTANDARD2_1
    public class Rfc3161TimestampTokenNetstandard21Wrapper : IRfc3161TimestampToken
    {

        private System.Security.Cryptography.Pkcs.Rfc3161TimestampToken _rfc3161TimestampToken = null;

        public Rfc3161TimestampTokenNetstandard21Wrapper(
            IRfc3161TimestampTokenInfo tstInfo,
            X509Certificate2 signerCertificate,
            X509Certificate2Collection additionalCerts,
            byte[] encoded)
        {
            bool success = System.Security.Cryptography.Pkcs.Rfc3161TimestampToken.TryDecode(
                new ReadOnlyMemory<byte>(encoded),
                out _rfc3161TimestampToken,
                out int bytesConsumed);

        }
        public Rfc3161TimestampTokenNetstandard21Wrapper(
            System.Security.Cryptography.Pkcs.Rfc3161TimestampToken rfc3161TimestampToken)
        {
            _rfc3161TimestampToken = rfc3161TimestampToken;
        }
        public IRfc3161TimestampTokenInfo TokenInfo
        {
            get
            {
                Rfc3161TimestampTokenInfoNetstandard21Wrapper TokenInfo = new Rfc3161TimestampTokenInfoNetstandard21Wrapper(_rfc3161TimestampToken.TokenInfo);
                return TokenInfo;
            }
        }

        public SignedCms AsSignedCms()
        {
            return _rfc3161TimestampToken.AsSignedCms();
        }
    }
#endif
}

