using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Packaging.Signing
{
#if IS_SIGNING_SUPPORTED && NETSTANDARD2_1
    internal class SignedCmsWrapper : ICms
    {
        private SignedCms _signedCms;

        public SignedCmsWrapper(SignedCms signedCms)
        {
            _signedCms = signedCms;
        }

        public byte[] GetPrimarySignatureSignatureValue()
        {
            return _signedCms.SignerInfos[0].GetSignature();
        }
        public byte[] GetRepositoryCountersignatureSignatureValue()
        {
            return _signedCms.SignerInfos[0].CounterSignerInfos[0].GetSignature();
        }

        public void AddCertificates(IEnumerable<byte[]> encodedCertificates)
        {
            foreach (var encodedCertificate in encodedCertificates)
            {
                _signedCms.AddCertificate(new X509Certificate2(encodedCertificate));
            }
            
        }

        public void AddCountersignature(CmsSigner cmsSigner, CngKey privateKey)
        {
            _signedCms.SignerInfos[0].ComputeCounterSignature(cmsSigner);
        }

        public void AddTimestampToRepositoryCountersignature(SignedCms timestamp)
        {
            var bytes = timestamp.Encode();

            var unsignedAttribute = new AsnEncodedData(Oids.SignatureTimeStampTokenAttribute, bytes);

            _signedCms.SignerInfos[0].CounterSignerInfos[0].AddUnsignedAttribute(unsignedAttribute);

        }

        public void AddTimestamp(SignedCms timestamp)
        {
            var bytes = timestamp.Encode();

            var unsignedAttribute = new AsnEncodedData(Oids.SignatureTimeStampTokenAttribute, bytes);

            _signedCms.SignerInfos[0].AddUnsignedAttribute(unsignedAttribute);
         
        }

        public byte[] Encode()
        {
            return _signedCms.Encode();
        }

        public void Dispose()
        {
           //TODO: complete the dispose method
        }
    }
#endif
}

