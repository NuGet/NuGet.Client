using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography;

namespace NuGet.Packaging.Signing
{
#if IS_SIGNING_SUPPORTED && NETSTANDARD2_1
    public class Rfc3161TimestampTokenInfoNetstandard21Wrapper : IRfc3161TimstampTokenInfo
    {
        private System.Security.Cryptography.Pkcs.Rfc3161TimestampTokenInfo _rfc3161TimestampTokenInfo = null;

        public Rfc3161TimestampTokenInfoNetstandard21Wrapper(byte[] timestampTokenInfo)
        {
            bool success = System.Security.Cryptography.Pkcs.Rfc3161TimestampTokenInfo.TryDecode(
                new ReadOnlyMemory<byte>(timestampTokenInfo),
                out _rfc3161TimestampTokenInfo,
                out int bytesConsumed);
        }
        public string PolicyId
        {
            get
            {
                return _rfc3161TimestampTokenInfo.PolicyId.ToString();
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
            if (hash == null)
                return false;

            //var value = Decoded.MessageImprint.HashedMessage;
            var value = _rfc3161TimestampTokenInfo.GetMessageHash().ToArray();


            if (hash.Length != value.Length)
            {
                return false;
            }

            return value.SequenceEqual(hash);
        }
    }
#endif
}
