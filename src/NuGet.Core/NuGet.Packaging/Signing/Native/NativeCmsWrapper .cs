using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Packaging.Signing
{
#if IS_SIGNING_SUPPORTED && IS_DESKTOP
    internal class NativeCmsWrapper : ICms
    {
        private NativeCms _nativeCms;

        public NativeCmsWrapper(NativeCms nativeCms)
        {
            _nativeCms = nativeCms;
        }
        public byte[] GetPrimarySignatureSignatureValue()
        {
            return _nativeCms.GetPrimarySignatureSignatureValue();
        }
        public byte[] GetRepositoryCountersignatureSignatureValue()
        {
            return _nativeCms.GetRepositoryCountersignatureSignatureValue();
        }

        //static NativeCms Decode(byte[] input);

        public void AddCertificates(IEnumerable<byte[]> encodedCertificates)
        {
            _nativeCms.AddCertificates(encodedCertificates);
        }

        public void AddCountersignature(CmsSigner cmsSigner, CngKey privateKey)
        {
            _nativeCms.AddCountersignature(cmsSigner, privateKey);
        }

        public void AddTimestampToRepositoryCountersignature(SignedCms timestamp)
        {
            _nativeCms.AddTimestampToRepositoryCountersignature(timestamp);
        }

        public void AddTimestamp(SignedCms timestamp)
        {
            _nativeCms.AddTimestamp(timestamp);
        }

        public byte[] Encode()
        {
            return _nativeCms.Encode();
        }

        public void Dispose()
        {
            _nativeCms.Dispose();
        }
    }
#endif
}

