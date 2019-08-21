using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Packaging.Signing
{
    public interface IRfc3161TimestampToken
    {
#if IS_SIGNING_SUPPORTED
        IRfc3161TimestampTokenInfo TokenInfo { get; }

        SignedCms AsSignedCms();

#endif
    }
}

