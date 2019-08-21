using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Packaging.Signing
{
#if IS_SIGNING_SUPPORTED && IS_DESKTOP
    public class Rfc3161TimestampTokenNet472Wrapper : IRfc3161TimestampToken
    {
        private NuGet.Packaging.Signing.Rfc3161TimestampToken _rfc3161TimestampToken;

        public Rfc3161TimestampTokenNet472Wrapper(
            IRfc3161TimestampTokenInfo tstInfo,
            X509Certificate2 signerCertificate,
            X509Certificate2Collection additionalCerts,
            byte[] encoded)
        {
            _rfc3161TimestampToken = new Rfc3161TimestampToken(
                tstInfo,
                signerCertificate,
                additionalCerts,
                encoded);

        }

        public IRfc3161TimestampTokenInfo TokenInfo
        {
            get
            {
                return _rfc3161TimestampToken.TokenInfo;
            }
        }

        public SignedCms AsSignedCms()
        {
            return _rfc3161TimestampToken.AsSignedCms();
        }
    }
#endif
}

