// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities;
using Org.BouncyCastle.X509;

namespace Test.Utility.Signing
{
    public static class SigningTestUtility
    {
        /// <summary>
        /// Create a self signed certificate with bouncy castle.
        /// </summary>
        public static X509Certificate2 GenerateCertificate(string subjectName, Action<X509V3CertificateGenerator> modifyGenerator)
        {
            if (string.IsNullOrEmpty(subjectName))
            {
                subjectName = "NuGetTest";
            }

            var random = new SecureRandom();
            var pairGenerator = new RsaKeyPairGenerator();
            var genParams = new KeyGenerationParameters(random, 1024);
            pairGenerator.Init(genParams);
            var pair = pairGenerator.GenerateKeyPair();

            // Create cert
            var certGen = new X509V3CertificateGenerator();
            certGen.SetSubjectDN(new X509Name($"CN={subjectName}"));
            certGen.SetIssuerDN(new X509Name($"CN={subjectName}"));

            certGen.SetNotAfter(DateTime.UtcNow.Add(TimeSpan.FromHours(1)));
            certGen.SetNotBefore(DateTime.UtcNow.Subtract(TimeSpan.FromHours(1)));
            certGen.SetPublicKey(pair.Public);

            var serialNumber = BigIntegers.CreateRandomInRange(BigInteger.One, BigInteger.ValueOf(long.MaxValue), random);
            certGen.SetSerialNumber(serialNumber);

            var subjectKeyIdentifier = new SubjectKeyIdentifier(SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(pair.Public));
            certGen.AddExtension(X509Extensions.SubjectKeyIdentifier.Id, false, subjectKeyIdentifier);
            certGen.AddExtension(X509Extensions.KeyUsage, true, new KeyUsage(KeyUsage.KeyCertSign));
            certGen.AddExtension(X509Extensions.BasicConstraints.Id, true, new BasicConstraints(false));

            // Allow changes
            modifyGenerator?.Invoke(certGen);

            var issuerPrivateKey = pair.Private;
            var signatureFactory = new Asn1SignatureFactory("SHA256WITHRSA", issuerPrivateKey, random);
            var certificate = certGen.Generate(signatureFactory);
            var certResult = new X509Certificate2(certificate.GetEncoded());

#if IS_DESKTOP
            certResult.PrivateKey = DotNetUtilities.ToRSA(pair.Private as RsaPrivateCrtKeyParameters);
#endif

            return certResult;
        }

#if IS_DESKTOP
        /// <summary>
        /// Convert a cert private key into a AsymmetricKeyParameter
        /// </summary>
        public static AsymmetricKeyParameter GetPrivateKeyParameter(X509Certificate2 cert)
        {
            return DotNetUtilities.GetKeyPair(cert.PrivateKey).Private;
        }
#endif

        /// <summary>
        /// Returns the public cert without the private key.
        /// </summary>
        public static X509Certificate2 GetPublicCert(X509Certificate2 cert)
        {
            return new X509Certificate2(cert.Export(X509ContentType.Cert));
        }
    }
}
