using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Packaging.Signing
{
    public class IRfc3161TimestampTokenFactory
    {
        public static IRfc3161TimestampToken CreateIRfc3161TimestampToken(
            IRfc3161TimestampTokenInfo tstInfo,
            X509Certificate2 signerCertificate,
            X509Certificate2Collection additionalCerts,
            byte[] encoded)
        {
            IRfc3161TimestampToken iRfc3161TimestampToken = null;
#if IS_DESKTOP
            iRfc3161TimestampToken = new Rfc3161TimestampTokenNet472Wrapper(
                                            tstInfo,
                                            signerCertificate,
                                            additionalCerts,
                                            encoded);
#endif

#if NETSTANDARD2_1
            iRfc3161TimestampToken = new Rfc3161TimestampTokenNetstandard21Wrapper(
                                            tstInfo,
                                            signerCertificate,
                                            additionalCerts,
                                            encoded);
#endif
            return iRfc3161TimestampToken;
        }
    }
}
