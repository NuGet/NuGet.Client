// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
#if IS_DESKTOP
using System.Security.Cryptography.Pkcs;
#endif
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Core;
using NuGet.Packaging.Signing;
using NuGet.Shared;
using NuGet.Test.Utility;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.X509.Extension;
using Xunit;

namespace Test.Utility.Signing
{
    public static class SigningTestUtility
    {
        private static readonly string _signatureLogPrefix = "Package '{0} {1}' from source '{2}':";

        /// <summary>
        /// Modification generator that can be passed to TestCertificate.Generate().
        /// The generator will change the certificate EKU to ClientAuth.
        /// </summary>
        public static Action<X509V3CertificateGenerator> CertificateModificationGeneratorForInvalidEkuCert = delegate (X509V3CertificateGenerator gen)
        {
            // any EKU besides CodeSigning
            var usages = new[] { KeyPurposeID.IdKPClientAuth };

            gen.AddExtension(
                X509Extensions.ExtendedKeyUsage.Id,
                critical: true,
                extensionValue: new ExtendedKeyUsage(usages));
        };

        /// <summary>
        /// Modification generator that can be passed to TestCertificate.Generate().
        /// The generator will change the certificate EKU to CodeSigning.
        /// </summary>
        public static Action<X509V3CertificateGenerator> CertificateModificationGeneratorForCodeSigningEkuCert = delegate (X509V3CertificateGenerator gen)
        {
            var usages = new[] { KeyPurposeID.IdKPCodeSigning };

            gen.AddExtension(
                X509Extensions.ExtendedKeyUsage.Id,
                critical: true,
                extensionValue: new ExtendedKeyUsage(usages));
        };

        /// <summary>
        /// Modification generator that can be passed to TestCertificate.Generate().
        /// The generator will create an expired certificate.
        /// </summary>
        public static Action<X509V3CertificateGenerator> CertificateModificationGeneratorExpiredCert = delegate (X509V3CertificateGenerator gen)
        {
            var usages = new[] { KeyPurposeID.IdKPCodeSigning };

            gen.AddExtension(
                X509Extensions.ExtendedKeyUsage.Id,
                critical: true,
                extensionValue: new ExtendedKeyUsage(usages));

            gen.SetNotBefore(DateTime.UtcNow.AddHours(-1));
            gen.SetNotAfter(DateTime.UtcNow.AddMinutes(-1));
        };

        /// <summary>
        /// Modification generator that can be passed to TestCertificate.Generate().
        /// The generator will create a certificate that is not yet valid.
        /// </summary>
        public static Action<X509V3CertificateGenerator> CertificateModificationGeneratorNotYetValidCert = delegate (X509V3CertificateGenerator gen)
        {
            var usages = new[] { KeyPurposeID.IdKPCodeSigning };

            gen.AddExtension(
                X509Extensions.ExtendedKeyUsage.Id,
                critical: true,
                extensionValue: new ExtendedKeyUsage(usages));

            var notBefore = DateTime.UtcNow.AddDays(1);
            var notAfter = notBefore.AddHours(1);

            gen.SetNotBefore(notBefore);
            gen.SetNotAfter(notAfter);
        };

        /// <summary>
        /// Modification generator that can be passed to TestCertificate.Generate().
        /// The generator will create a certificate that is valid but will expire in 10 seconds.
        /// </summary>
        public static Action<X509V3CertificateGenerator> CertificateModificationGeneratorExpireIn10Seconds = delegate (X509V3CertificateGenerator gen)
        {
            var usages = new[] { KeyPurposeID.IdKPCodeSigning };

            gen.AddExtension(
                X509Extensions.ExtendedKeyUsage.Id,
                critical: true,
                extensionValue: new ExtendedKeyUsage(usages));

            var notBefore = DateTime.UtcNow.AddHours(-1);
            var notAfter = DateTime.UtcNow.AddSeconds(10);

            gen.SetNotBefore(notBefore);
            gen.SetNotAfter(notAfter);
        };

        /// <summary>
        /// Generates a list of certificates representing a chain of certificates.
        /// The first certificate is the root certificate stored in StoreName.Root and StoreLocation.LocalMachine.
        /// The last certificate is the leaf certificate stored in StoreName.TrustedPeople and StoreLocation.LocalMachine.
        /// Please dispose all the certificates in the list after use.
        /// </summary>
        /// <param name="length">Length of the chain.</param>
        /// <param name="crlServerUri">Uri for crl server</param>
        /// <param name="crlLocalUri">Uri for crl local</param>
        /// <param name="configureLeafCrl">Indicates if leaf crl should be configured</param>
        /// <returns>List of certificates representing a chain of certificates.</returns>
        public static IList<TrustedTestCert<TestCertificate>> GenerateCertificateChain(int length, string crlServerUri, string crlLocalUri, bool configureLeafCrl = true)
        {
            var certChain = new List<TrustedTestCert<TestCertificate>>();
            var actionGenerator = CertificateModificationGeneratorForCodeSigningEkuCert;
            TrustedTestCert<TestCertificate> issuer = null;
            TrustedTestCert<TestCertificate> cert = null;

            for (var i = 0; i < length; i++)
            {
                if (i == 0) // root CA cert
                {
                    var chainCertificateRequest = new ChainCertificateRequest()
                    {
                        CrlLocalBaseUri = crlLocalUri,
                        CrlServerBaseUri = crlServerUri,
                        IsCA = true
                    };

                    cert = TestCertificate.Generate(actionGenerator, chainCertificateRequest).WithPrivateKeyAndTrust(StoreName.Root, StoreLocation.LocalMachine);
                    issuer = cert;
                }
                else if (i < length - 1) // intermediate CA cert
                {
                    var chainCertificateRequest = new ChainCertificateRequest()
                    {
                        CrlLocalBaseUri = crlLocalUri,
                        CrlServerBaseUri = crlServerUri,
                        IsCA = true,
                        Issuer = issuer.Source.Cert
                    };

                    cert = TestCertificate.Generate(actionGenerator, chainCertificateRequest).WithPrivateKeyAndTrust(StoreName.CertificateAuthority, StoreLocation.LocalMachine);
                    issuer = cert;
                }
                else // leaf cert
                {
                    var chainCertificateRequest = new ChainCertificateRequest()
                    {
                        CrlLocalBaseUri = crlLocalUri,
                        CrlServerBaseUri = crlServerUri,
                        IsCA = false,
                        ConfigureCrl = configureLeafCrl,
                        Issuer = issuer.Source.Cert
                    };

                    cert = TestCertificate.Generate(actionGenerator, chainCertificateRequest).WithPrivateKeyAndTrust(StoreName.My, StoreLocation.LocalMachine);
                }

                certChain.Add(cert);
            }

            return certChain;
        }

        /// <summary>
        /// Create a self signed certificate with bouncy castle.
        /// </summary>
        public static X509Certificate2 GenerateCertificate(
            string subjectName,
            Action<X509V3CertificateGenerator> modifyGenerator,
            string signatureAlgorithm = "SHA256WITHRSA",
            int publicKeyLength = 2048,
            ChainCertificateRequest chainCertificateRequest = null)
        {
            if (string.IsNullOrEmpty(subjectName))
            {
                subjectName = "NuGetTest";
            }

            var random = new SecureRandom();
            var keyPair = GenerateKeyPair(publicKeyLength);

            // Create cert
            var subjectDN = $"CN={subjectName}";
            var certGen = new X509V3CertificateGenerator();
            certGen.SetSubjectDN(new X509Name(subjectDN));

            // default to new key pair
            var issuerPrivateKey = keyPair.Private;
            var keyUsage = KeyUsage.DigitalSignature;
            var issuerDN = chainCertificateRequest?.IssuerDN ?? subjectDN;
            certGen.SetIssuerDN(new X509Name(issuerDN));
            
#if IS_DESKTOP
            if (chainCertificateRequest != null)
            {
                if (chainCertificateRequest.Issuer != null)
                {
                    // for a certificate with an issuer assign Authority Key Identifier
                    var issuer = chainCertificateRequest?.Issuer;
                    var bcIssuer = DotNetUtilities.FromX509Certificate(issuer);
                    var authorityKeyIdentifier = new AuthorityKeyIdentifierStructure(bcIssuer);
                    issuerPrivateKey = DotNetUtilities.GetKeyPair(issuer.PrivateKey).Private;
                    certGen.AddExtension(X509Extensions.AuthorityKeyIdentifier.Id, false, authorityKeyIdentifier);
                }

                if (chainCertificateRequest.ConfigureCrl)
                {
                    // for a certificate in a chain create CRL distribution point extension
                    var crlServerUri = $"{chainCertificateRequest.CrlServerBaseUri}{issuerDN}.crl";
                    var generalName = new Org.BouncyCastle.Asn1.X509.GeneralName(Org.BouncyCastle.Asn1.X509.GeneralName.UniformResourceIdentifier, new DerIA5String(crlServerUri));
                    var distPointName = new DistributionPointName(new GeneralNames(generalName));
                    var distPoint = new DistributionPoint(distPointName, null, null);

                    certGen.AddExtension(X509Extensions.CrlDistributionPoints, critical: false, extensionValue: new DerSequence(distPoint));
                }

                if (chainCertificateRequest.IsCA)
                {
                    // update key usage with CA cert sign and crl sign attributes
                    keyUsage |= KeyUsage.CrlSign | KeyUsage.KeyCertSign;
                }
            }
#endif
            certGen.SetNotAfter(DateTime.UtcNow.Add(TimeSpan.FromHours(1)));
            certGen.SetNotBefore(DateTime.UtcNow.Subtract(TimeSpan.FromHours(1)));
            certGen.SetPublicKey(keyPair.Public);

            var serialNumber = BigIntegers.CreateRandomInRange(BigInteger.One, BigInteger.ValueOf(long.MaxValue), random);
            certGen.SetSerialNumber(serialNumber);

            var subjectKeyIdentifier = new SubjectKeyIdentifier(SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(keyPair.Public));
            certGen.AddExtension(X509Extensions.SubjectKeyIdentifier.Id, false, subjectKeyIdentifier);

            certGen.AddExtension(X509Extensions.KeyUsage.Id, false, new KeyUsage(keyUsage));
            certGen.AddExtension(X509Extensions.BasicConstraints.Id, true, new BasicConstraints(chainCertificateRequest?.IsCA ?? false));

            // Allow changes
            modifyGenerator?.Invoke(certGen);

            var signatureFactory = new Asn1SignatureFactory(signatureAlgorithm, issuerPrivateKey, random);
            var certificate = certGen.Generate(signatureFactory);
            var certResult = new X509Certificate2(certificate.GetEncoded());

#if IS_DESKTOP
            certResult.PrivateKey = DotNetUtilities.ToRSA(keyPair.Private as RsaPrivateCrtKeyParameters);
#endif

            return certResult;
        }

        /// <summary>
        /// Create a self signed certificate.
        /// </summary>
        public static X509Certificate2 GenerateCertificate(
            string subjectName,
            AsymmetricCipherKeyPair keyPair)
        {
            if (string.IsNullOrEmpty(subjectName))
            {
                subjectName = "NuGetTest";
            }

            var certGen = new X509V3CertificateGenerator();
            certGen.SetSubjectDN(new X509Name($"CN={subjectName}"));
            certGen.SetIssuerDN(new X509Name($"CN={subjectName}"));

            certGen.SetNotAfter(DateTime.UtcNow.Add(TimeSpan.FromHours(1)));
            certGen.SetNotBefore(DateTime.UtcNow.Subtract(TimeSpan.FromHours(1)));
            certGen.SetPublicKey(keyPair.Public);

            var random = new SecureRandom();
            var serialNumber = BigIntegers.CreateRandomInRange(BigInteger.One, BigInteger.ValueOf(long.MaxValue), random);

            certGen.SetSerialNumber(serialNumber);

            var subjectKeyIdentifier = new SubjectKeyIdentifier(SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(keyPair.Public));
            certGen.AddExtension(X509Extensions.SubjectKeyIdentifier.Id, false, subjectKeyIdentifier);
            certGen.AddExtension(X509Extensions.KeyUsage, true, new KeyUsage(KeyUsage.KeyCertSign));
            certGen.AddExtension(X509Extensions.BasicConstraints.Id, true, new BasicConstraints(false));

            var usages = new[] { KeyPurposeID.IdKPCodeSigning };

            certGen.AddExtension(
                X509Extensions.ExtendedKeyUsage.Id,
                critical: true,
                extensionValue: new ExtendedKeyUsage(usages));

            var issuerPrivateKey = keyPair.Private;
            var signatureFactory = new Asn1SignatureFactory("SHA256WITHRSA", issuerPrivateKey, random);
            var certificate = certGen.Generate(signatureFactory);
            var certResult = new X509Certificate2(certificate.GetEncoded());

#if IS_DESKTOP
            certResult.PrivateKey = DotNetUtilities.ToRSA(keyPair.Private as RsaPrivateCrtKeyParameters);
#endif

            return certResult;
        }

        public static X509Certificate2 GenerateCertificate(
            string issuerName,
            string subjectName,
            AsymmetricKeyParameter issuerPrivateKey,
            AsymmetricCipherKeyPair keyPair)
        {
            var certGen = new X509V3CertificateGenerator();
            certGen.SetSubjectDN(new X509Name($"CN={subjectName}"));
            certGen.SetIssuerDN(new X509Name($"CN={issuerName}"));

            certGen.SetNotAfter(DateTime.UtcNow.Add(TimeSpan.FromHours(1)));
            certGen.SetNotBefore(DateTime.UtcNow.Subtract(TimeSpan.FromHours(1)));
            certGen.SetPublicKey(keyPair.Public);

            var random = new SecureRandom();
            var serialNumber = BigIntegers.CreateRandomInRange(BigInteger.One, BigInteger.ValueOf(long.MaxValue), random);

            certGen.SetSerialNumber(serialNumber);

            var subjectKeyIdentifier = new SubjectKeyIdentifier(SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(keyPair.Public));
            certGen.AddExtension(X509Extensions.SubjectKeyIdentifier.Id, false, subjectKeyIdentifier);
            certGen.AddExtension(X509Extensions.KeyUsage, true, new KeyUsage(KeyUsage.KeyCertSign));
            certGen.AddExtension(X509Extensions.BasicConstraints.Id, true, new BasicConstraints(false));

            var usages = new[] { KeyPurposeID.IdKPCodeSigning };

            certGen.AddExtension(
                X509Extensions.ExtendedKeyUsage.Id,
                critical: true,
                extensionValue: new ExtendedKeyUsage(usages));

            var signatureFactory = new Asn1SignatureFactory("SHA256WITHRSA", issuerPrivateKey, random);
            var certificate = certGen.Generate(signatureFactory);
            var certResult = new X509Certificate2(certificate.GetEncoded());

#if IS_DESKTOP
            certResult.PrivateKey = DotNetUtilities.ToRSA(keyPair.Private as RsaPrivateCrtKeyParameters);
#endif

            return certResult;
        }

        public static X509Certificate2 GenerateSelfIssuedCertificate(bool isCa)
        {
            var keyPair = GenerateKeyPair(publicKeyLength: 2048);
            var generator = new X509V3CertificateGenerator();
            var keyUsages = KeyUsage.DigitalSignature | KeyUsage.KeyCertSign | KeyUsage.CrlSign;

            if (isCa)
            {
                keyUsages |= KeyUsage.KeyCertSign;
            }

            generator.AddExtension(
                X509Extensions.SubjectKeyIdentifier,
                critical: false,
                extensionValue: new SubjectKeyIdentifierStructure(keyPair.Public));
            generator.AddExtension(
                X509Extensions.AuthorityKeyIdentifier,
                critical: false,
                extensionValue: new AuthorityKeyIdentifierStructure(keyPair.Public));
            generator.AddExtension(
                X509Extensions.BasicConstraints,
                critical: true,
                extensionValue: new BasicConstraints(cA: isCa));
            generator.AddExtension(
                X509Extensions.KeyUsage,
                critical: true,
                extensionValue: new KeyUsage(keyUsages));

            var subjectName = new X509Name($"C=US,ST=WA,L=Redmond,O=NuGet,CN=NuGet Test Self-Issued Certificate ({Guid.NewGuid().ToString()})");
            var now = DateTime.UtcNow;

            generator.SetSerialNumber(BigInteger.One);
            generator.SetIssuerDN(subjectName);
            generator.SetNotBefore(now);
            generator.SetNotAfter(now.AddHours(1));
            generator.SetSubjectDN(subjectName);
            generator.SetPublicKey(keyPair.Public);

            var signatureFactory = new Asn1SignatureFactory("SHA256WITHRSA", keyPair.Private);
            var bcCertificate = generator.Generate(signatureFactory);

            var certificate = new X509Certificate2(bcCertificate.GetEncoded());

#if IS_DESKTOP
            certificate.PrivateKey = DotNetUtilities.ToRSA(keyPair.Private as RsaPrivateCrtKeyParameters);
#endif

            return certificate;
        }

        private static X509SubjectKeyIdentifierExtension GetSubjectKeyIdentifier(X509Certificate2 issuer)
        {
            var subjectKeyIdentifierOid = "2.5.29.14";

            foreach (var extension in issuer.Extensions)
            {
                if (string.Equals(extension.Oid.Value, subjectKeyIdentifierOid))
                {
                    return extension as X509SubjectKeyIdentifierExtension;
                }
            }

            return null;
        }

        public static AsymmetricCipherKeyPair GenerateKeyPair(int publicKeyLength)
        {
            var random = new SecureRandom();
            var keyPairGenerator = new RsaKeyPairGenerator();
            var parameters = new KeyGenerationParameters(random, publicKeyLength);

            keyPairGenerator.Init(parameters);

            return keyPairGenerator.GenerateKeyPair();
        }

#if IS_DESKTOP
        /// <summary>
        /// Generates a SignedCMS object for some content.
        /// </summary>
        /// <param name="content"></param>
        /// <param name="cert">Certificate for cms signer</param>
        /// <returns>SignedCms object</returns>
        public static SignedCms GenerateSignedCms(X509Certificate2 cert, byte[] content)
        {
            var contentInfo = new ContentInfo(content);
            var cmsSigner = new CmsSigner(SubjectIdentifierType.SubjectKeyIdentifier, cert);
            var signingTime = new Pkcs9SigningTime();

            cmsSigner.SignedAttributes.Add(
                new CryptographicAttributeObject(
                    signingTime.Oid,
                    new AsnEncodedDataCollection(signingTime)));

            var cms = new SignedCms(contentInfo);
            cms.ComputeSignature(cmsSigner);

            return cms;
        }

        /// <summary>
        /// Generates a SignedCMS object for some content.
        /// </summary>
        /// <param name="content"></param>
        /// <param name="cert">Certificate for cms signer</param>
        /// <returns>SignedCms object</returns>
        public static SignedCms GenerateRepositoryCountersignedSignedCms(X509Certificate2 cert, byte[] content)
        {
            var contentInfo = new ContentInfo(content);
            var hashAlgorithm = NuGet.Common.HashAlgorithmName.SHA256;

            using (var primarySignatureRequest = new AuthorSignPackageRequest(new X509Certificate2(cert), hashAlgorithm))
            using (var countersignatureRequest = new RepositorySignPackageRequest(new X509Certificate2(cert), hashAlgorithm, hashAlgorithm, new Uri("https://api.nuget.org/v3/index.json"), null))
            {
                var cmsSigner = SigningUtility.CreateCmsSigner(primarySignatureRequest, NullLogger.Instance);

                var cms = new SignedCms(contentInfo);
                cms.ComputeSignature(cmsSigner);

                var counterCmsSigner = SigningUtility.CreateCmsSigner(countersignatureRequest, NullLogger.Instance);
                cms.SignerInfos[0].ComputeCounterSignature(counterCmsSigner);

                return cms;
            }
        }
#endif

        /// <summary>
        /// Returns the public cert without the private key.
        /// </summary>
        public static X509Certificate2 GetPublicCert(X509Certificate2 cert)
        {
            return new X509Certificate2(cert.Export(X509ContentType.Cert));
        }

        /// <summary>
        /// Returns the public cert with the private key.
        /// </summary>
        public static X509Certificate2 GetPublicCertWithPrivateKey(X509Certificate2 cert)
        {
            var password = new Guid().ToString();
            return new X509Certificate2(cert.Export(X509ContentType.Pfx, password), password, X509KeyStorageFlags.PersistKeySet|X509KeyStorageFlags.Exportable);
        }

        public static TrustedTestCert<TestCertificate> GenerateTrustedTestCertificate()
        {
            var actionGenerator = CertificateModificationGeneratorForCodeSigningEkuCert;

            // Code Sign EKU needs trust to a root authority
            // Add the cert to Root CA list in LocalMachine as it does not prompt a dialog
            // This makes all the associated tests to require admin privilege
            return TestCertificate.Generate(actionGenerator).WithTrust(StoreName.Root, StoreLocation.LocalMachine);
        }

        public static TrustedTestCert<TestCertificate> GenerateTrustedTestCertificateExpired()
        {
            var actionGenerator = CertificateModificationGeneratorExpiredCert;

            // Code Sign EKU needs trust to a root authority
            // Add the cert to Root CA list in LocalMachine as it does not prompt a dialog
            // This makes all the associated tests to require admin privilege
            return TestCertificate.Generate(actionGenerator).WithTrust(StoreName.Root, StoreLocation.LocalMachine);
        }

        public static TrustedTestCert<TestCertificate> GenerateTrustedTestCertificateNotYetValid()
        {
            var actionGenerator = CertificateModificationGeneratorNotYetValidCert;

            // Code Sign EKU needs trust to a root authority
            // Add the cert to Root CA list in LocalMachine as it does not prompt a dialog
            // This makes all the associated tests to require admin privilege
            return TestCertificate.Generate(actionGenerator).WithTrust(StoreName.Root, StoreLocation.LocalMachine);
        }

        public static TrustedTestCert<TestCertificate> GenerateTrustedTestCertificateThatExpiresIn10Seconds()
        {
            var actionGenerator = CertificateModificationGeneratorExpireIn10Seconds;

            // Code Sign EKU needs trust to a root authority
            // Add the cert to Root CA list in LocalMachine as it does not prompt a dialog
            // This makes all the associated tests to require admin privilege
            return TestCertificate.Generate(actionGenerator).WithTrust(StoreName.Root, StoreLocation.LocalMachine);
        }

        public static bool AreVerifierSettingsEqual(SignedPackageVerifierSettings first, SignedPackageVerifierSettings second)
        {
            return first.AllowIgnoreTimestamp == second.AllowIgnoreTimestamp &&
                first.AllowIllegal == second.AllowIllegal &&
                first.AllowMultipleTimestamps == second.AllowMultipleTimestamps &&
                first.AllowNoTimestamp == second.AllowNoTimestamp &&
                first.AllowUnknownRevocation == second.AllowUnknownRevocation &&
                first.ReportUnknownRevocation == second.ReportUnknownRevocation &&
                first.AllowUnsigned == second.AllowUnsigned &&
                first.AllowUntrusted == second.AllowUntrusted &&
                first.VerificationTarget == second.VerificationTarget &&
                first.SignaturePlacement == second.SignaturePlacement &&
                first.RepositoryCountersignatureVerificationBehavior == second.RepositoryCountersignatureVerificationBehavior;
        }

#if IS_DESKTOP
        public static DisposableList<IDisposable> RegisterDefaultResponders(
            this ISigningTestServer testServer,
            TimestampService timestampService)
        {
            var responders = new DisposableList<IDisposable>();
            var ca = timestampService.CertificateAuthority;

            while (ca != null)
            {
                responders.Add(testServer.RegisterResponder(ca));
                responders.Add(testServer.RegisterResponder(ca.OcspResponder));

                ca = ca.Parent;
            }

            responders.Add(testServer.RegisterResponder(timestampService));

            return responders;
        }
#endif

        public static async Task<VerifySignaturesResult> VerifySignatureAsync(SignedPackageArchive signPackage, SignedPackageVerifierSettings settings)
        {
            var verificationProviders = new[] { new SignatureTrustAndValidityVerificationProvider() };
            var verifier = new PackageSignatureVerifier(verificationProviders);
            var result = await verifier.VerifySignaturesAsync(signPackage, settings, CancellationToken.None);
            return result;
        }

        public static byte[] GetResourceBytes(string name)
        {
            return ResourceTestUtility.GetResourceBytes($"Test.Utility.compiler.resources.{name}", typeof(SigningTestUtility));
        }

        public static X509Certificate2 GetCertificate(string name)
        {
            var bytes = GetResourceBytes(name);

            return new X509Certificate2(bytes);
        }

        public static byte[] GetHash(X509Certificate2 certificate, NuGet.Common.HashAlgorithmName hashAlgorithm)
        {
            return hashAlgorithm.ComputeHash(certificate.RawData);
        }

        public static void VerifySerialNumber(X509Certificate2 certificate, NuGet.Packaging.Signing.IssuerSerial issuerSerial)
        {
            var serialNumber = certificate.GetSerialNumber();

            // Convert from little endian to big endian.
            Array.Reverse(serialNumber);

            VerifyByteArrays(serialNumber, issuerSerial.SerialNumber);
        }

        public static void VerifyByteArrays(byte[] expected, byte[] actual)
        {
            var expectedHex = BitConverter.ToString(expected).Replace("-", "");
            var actualHex = BitConverter.ToString(actual).Replace("-", "");

            Assert.Equal(expectedHex, actualHex);
        }

        public static void AssertOfflineRevocation(IEnumerable<ILogMessage> issues, LogLevel logLevel)
        {
            string offlineRevocation;

            if (RuntimeEnvironmentHelper.IsWindows)
            {
                offlineRevocation = "The revocation function was unable to check revocation because the revocation server was offline.";
            }
            else
            {
                offlineRevocation = "unable to get certificate CRL";
            }

            Assert.Contains(issues, issue =>
                issue.Code == NuGetLogCode.NU3018 &&
                issue.Level == logLevel &&
                issue.Message == offlineRevocation);
        }

        public static void AssertRevocationStatusUnknown(IEnumerable<ILogMessage> issues, LogLevel logLevel)
        {
            Assert.Contains(issues, issue =>
                issue.Code == NuGetLogCode.NU3018 &&
                issue.Level == logLevel &&
                issue.Message == "The revocation function was unable to check revocation for the certificate.");
        }

        public static void AssertUntrustedRoot(IEnumerable<ILogMessage> issues, LogLevel logLevel)
        {
            string untrustedRoot;

            if (RuntimeEnvironmentHelper.IsWindows)
            {
                untrustedRoot = "A certificate chain processed, but terminated in a root certificate which is not trusted by the trust provider.";
            }
            else
            {
                untrustedRoot = "certificate not trusted";
            }

            Assert.Contains(issues, issue =>
                issue.Code == NuGetLogCode.NU3018 &&
                issue.Level == logLevel &&
                issue.Message == untrustedRoot);
        }

        public static string AddSignatureLogPrefix(string log, PackageIdentity package, string source)
        {
            return $"{string.Format(_signatureLogPrefix, package.Id, package.Version, source)} {log}";
        }
    }
}