using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Packaging.Signing
{
    public class IRfc3161TSTFactory
    {
        public static IRfc3161TimstampTokenInfo CreateTSTInfo(byte[] bytes)
        {
            IRfc3161TimstampTokenInfo iRfc3161TSTInfo = null;
#if IS_DESKTOP
            iRfc3161TSTInfo = new Rfc3161TimestampTokenInfoNet472Wrapper(bytes);
#endif

#if NETSTANDARD2_1
            iRfc3161TSTInfo = new Rfc3161TimestampTokenInfoNetstandard21Wrapper(bytes);
#endif
            return iRfc3161TSTInfo;
        }
    }
}
