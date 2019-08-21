using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Packaging.Signing
{
#if IS_SIGNING_SUPPORTED && IS_DESKTOP
    public class Rfc3161TimestampTokenInfoNet472Wrapper : IRfc3161TimestampTokenInfo
    {
        private NuGet.Packaging.Signing.Rfc3161TimestampTokenInfo _rfc3161TimestampTokenInfo;

        public Rfc3161TimestampTokenInfoNet472Wrapper(byte[] timestampTokenInfo)
        {
            _rfc3161TimestampTokenInfo = new Rfc3161TimestampTokenInfo(timestampTokenInfo);
        }

        public Rfc3161TimestampTokenInfoNet472Wrapper(NuGet.Packaging.Signing.Rfc3161TimestampTokenInfo timestampTokenInfo)
        {
            _rfc3161TimestampTokenInfo = timestampTokenInfo;
        }
        public string PolicyId
        {
            get
            {
                return _rfc3161TimestampTokenInfo.PolicyId;
            }   
        }

        public DateTimeOffset Timestamp
        {
            get
            {
                return _rfc3161TimestampTokenInfo.Timestamp;
            }
        }

        public long? AccuracyInMicroseconds
        {
            get
            {
                return _rfc3161TimestampTokenInfo.AccuracyInMicroseconds;
            }
        }

        public Oid HashAlgorithmId
        {
            get
            {
                return _rfc3161TimestampTokenInfo.HashAlgorithmId;
            }
        }

        public bool HasMessageHash(byte[] hash)
        {
            return _rfc3161TimestampTokenInfo.HasMessageHash(hash);
        }
        public byte[] GetNonce()
        {
            return _rfc3161TimestampTokenInfo.GetNonce();
        }
    }
#endif
}
