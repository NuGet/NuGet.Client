// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Prng;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;

namespace NuGet.Packaging.FuncTest
{
    /// <summary>
    /// Class to allow generation of test certificate
    /// </summary>
    public static class TestCertificateUtility
    {
        public static X509Certificate2 GenerateDotNetCertificate(
            string certName,
            DateTime beginTime,
            DateTime expiryTime)
        {
            var newCert = GenerateBouncyCastleCertificate(certName, GenerateKeyPair(), beginTime, expiryTime);

            var dn = new X509Certificate2();
            dn.Import(newCert.GetEncoded());

            return new X509Certificate2(DotNetUtilities.ToX509Certificate(newCert));
        }

        public static Org.BouncyCastle.X509.X509Certificate GenerateBouncyCastleCertificate(
            string certName,
            DateTime beginTime,
            DateTime expiryTime)
        {
            return GenerateBouncyCastleCertificate(certName, GenerateKeyPair(), beginTime, expiryTime);
        }

        public static Org.BouncyCastle.X509.X509Certificate GenerateBouncyCastleCertificate(
            string certName,
            AsymmetricCipherKeyPair keyPair,
            DateTime beginTime,
            DateTime expiryTime)
        {
            var random = GenerateSecureRandomGenerator();
            var signatureFactory = new Asn1SignatureFactory("SHA512WITHRSA", keyPair.Private, random);
            var gen = new X509V3CertificateGenerator();

            var CN = new X509Name("CN=" + certName);

            gen.SetSerialNumber(new Org.BouncyCastle.Math.BigInteger("100"));
            gen.SetSubjectDN(CN);
            gen.SetIssuerDN(CN);
            gen.SetNotAfter(expiryTime);
            gen.SetNotBefore(beginTime);
            gen.SetPublicKey(keyPair.Public);

            return gen.Generate(signatureFactory);
        }

        public static AsymmetricCipherKeyPair GenerateKeyPair()
        {
            var keypairgen = new RsaKeyPairGenerator();
            keypairgen.Init(new KeyGenerationParameters(new SecureRandom(new CryptoApiRandomGenerator()), 1024));

            return keypairgen.GenerateKeyPair();
        }

        private static SecureRandom GenerateSecureRandomGenerator()
        {
            var randomGenerator = new CryptoApiRandomGenerator();
            var random = new SecureRandom(randomGenerator);
            return random;
        }
    }
}
