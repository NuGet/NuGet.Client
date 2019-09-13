using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.Pkcs;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Packaging.Signing
{
    public class ICamFactory
    {
        internal static ICms CreateICms(byte[] input)
        {
            ICms cms = null;
#if IS_DESKTOP
            NativeCms nativeCms = NativeCms.Decode(input);
            cms = new NativeCmsWrapper(nativeCms);
#endif

#if NETSTANDARD2_1
            SignedCms signedCms = new SignedCms();
            signedCms.Decode(input);
            cms = new SignedCmsWrapper(signedCms);
#endif
            return cms;
        }

    }
}
