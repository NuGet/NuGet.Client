// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Core;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509.Extension;
using Xunit;
using X509Extension = System.Security.Cryptography.X509Certificates.X509Extension;

namespace Test.Utility.Signing
{
    public static class SigningTestUtility
    {
        private static readonly string _signatureLogPrefix = "Package '{0} {1}' from source '{2}':";

        /// <summary>
        /// Modification generator that can be passed to TestCertificate.Generate().
        /// The generator will change the certificate EKU to ClientAuth.
        /// </summary>
        public static Action<TestCertificateGenerator> CertificateModificationGeneratorForInvalidEkuCert = delegate (TestCertificateGenerator gen)
        {
            // any EKU besides CodeSigning
            var usages = new OidCollection { new Oid(TestOids.IdKpClientAuth) };

            gen.Extensions.Add(
                 new X509EnhancedKeyUsageExtension(
                     usages,
                     critical: true));
        };

        /// <summary>
        /// Modification generator that can be passed to TestCertificate.Generate().
        /// The generator will change the certificate EKU to CodeSigning.
        /// </summary>
        public static Action<TestCertificateGenerator> CertificateModificationGeneratorForCodeSigningEkuCert = delegate (TestCertificateGenerator gen)
        {
            var usages = new OidCollection { new Oid(Oids.CodeSigningEku) };

            gen.Extensions.Add(
                  new X509EnhancedKeyUsageExtension(
                      usages,
                      critical: true));
        };

        /// <summary>
        /// Modification generator that can be passed to TestCertificate.Generate().
        /// The generator will create an expired certificate.
        /// </summary>
        public static Action<TestCertificateGenerator> CertificateModificationGeneratorExpiredCert = delegate (TestCertificateGenerator gen)
        {
            var usages = new OidCollection { new Oid(Oids.CodeSigningEku) };

            gen.Extensions.Add(
                  new X509EnhancedKeyUsageExtension(
                      usages,
                      critical: true));

            gen.NotBefore = DateTime.UtcNow.AddHours(-1);
            gen.NotAfter = DateTime.UtcNow.AddMinutes(-1);
        };

        /// <summary>
        /// Modification generator that can be passed to TestCertificate.Generate().
        /// The generator will create a certificate that is not yet valid.
        /// </summary>
        public static Action<TestCertificateGenerator> CertificateModificationGeneratorNotYetValidCert = delegate (TestCertificateGenerator gen)
        {
            var usages = new OidCollection { new Oid(Oids.CodeSigningEku) };

            gen.Extensions.Add(
             new X509EnhancedKeyUsageExtension(
                 usages,
                 critical: true));

            var notBefore = DateTime.UtcNow.AddDays(1);

            gen.NotBefore = notBefore;
            gen.NotAfter = notBefore.AddHours(1);
        };

        /// <summary>
        /// Modification generator that can be passed to TestCertificate.Generate().
        /// The generator will create a certificate that is valid but will expire in 10 seconds.
        /// </summary>
        public static Action<TestCertificateGenerator> CertificateModificationGeneratorExpireIn10Seconds = delegate (TestCertificateGenerator gen)
        {
            var usages = new OidCollection { new Oid(Oids.CodeSigningEku) };

            gen.Extensions.Add(
                new X509EnhancedKeyUsageExtension(
                    usages,
                    critical: true));

            gen.NotBefore = DateTime.UtcNow.AddHours(-1);
            gen.NotAfter = DateTime.UtcNow.AddSeconds(10);
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
        /// <param name="dir">Test directory to store certificates if needed</param>
        /// <param name="configureLeafCrl">Indicates if leaf crl should be configured</param>
        /// <returns>List of certificates representing a chain of certificates.</returns>
        public static IList<TrustedTestCert<TestCertificate>> GenerateCertificateChain(int length, string crlServerUri, string crlLocalUri, TestDirectory dir, bool configureLeafCrl = true)
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

                    cert = TestCertificate.Generate(actionGenerator, chainCertificateRequest).WithPrivateKeyAndTrust(StoreName.Root, StoreLocation.LocalMachine, dir);
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

                    cert = TestCertificate.Generate(actionGenerator, chainCertificateRequest).WithPrivateKeyAndTrust(StoreName.CertificateAuthority, StoreLocation.LocalMachine, dir);
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

                    cert = TestCertificate.Generate(actionGenerator, chainCertificateRequest).WithPrivateKeyAndTrust(StoreName.My, StoreLocation.LocalMachine, dir);
                }

                certChain.Add(cert);
            }

            return certChain;
        }

        public static X509CertificateWithKeyInfo GenerateCertificateWithKeyInfo(
            string subjectName,
            Action<TestCertificateGenerator> modifyGenerator,
            NuGet.Common.HashAlgorithmName hashAlgorithm = NuGet.Common.HashAlgorithmName.SHA256,
            RSASignaturePaddingMode paddingMode = RSASignaturePaddingMode.Pkcs1,
            int publicKeyLength = 2048,
            ChainCertificateRequest chainCertificateRequest = null)
        {
            var rsa = RSA.Create(publicKeyLength);
            var cert = GenerateCertificate(subjectName, modifyGenerator, rsa, hashAlgorithm, paddingMode, chainCertificateRequest);

            return new X509CertificateWithKeyInfo(cert, rsa);
        }

        /// <summary>
        /// Create a self signed certificate with bouncy castle.
        /// </summary>
        public static X509Certificate2 GenerateCertificate(
            string subjectName,
            Action<TestCertificateGenerator> modifyGenerator,
            NuGet.Common.HashAlgorithmName hashAlgorithm = NuGet.Common.HashAlgorithmName.SHA256,
            RSASignaturePaddingMode paddingMode = RSASignaturePaddingMode.Pkcs1,
            int publicKeyLength = 2048,
            ChainCertificateRequest chainCertificateRequest = null)
        {
            using (var rsa = RSA.Create(publicKeyLength))
            {
                return GenerateCertificate(subjectName, modifyGenerator, rsa, hashAlgorithm, paddingMode, chainCertificateRequest);
            }
        }

        private static X509Certificate2 GenerateCertificate(
            string subjectName,
            Action<TestCertificateGenerator> modifyGenerator,
            RSA rsa,
            NuGet.Common.HashAlgorithmName hashAlgorithm,
            RSASignaturePaddingMode paddingMode,
            ChainCertificateRequest chainCertificateRequest)
        {
            if (string.IsNullOrEmpty(subjectName))
            {
                subjectName = $"NuGet Test Self-Issued Certificate ({Guid.NewGuid().ToString()})";
            }

            // Create cert
            var subjectDN = $"C=US,S=WA,L=Redmond,O=NuGet,CN={subjectName}";
            var certGen = new TestCertificateGenerator();

            var isSelfSigned = true;
            var isCA = false;
            X509Certificate2 issuer = null;
            DateTimeOffset? notAfter = null;
            RsaKeyParameters issuerPublicKey = null;

            var keyUsage = X509KeyUsageFlags.DigitalSignature;

            if (chainCertificateRequest != null)
            {
                if (chainCertificateRequest.Issuer != null)
                {
                    isSelfSigned = false;
                    // for a certificate with an issuer assign Authority Key Identifier
                    issuer = chainCertificateRequest?.Issuer;

                    notAfter = issuer.NotAfter.Subtract(TimeSpan.FromMinutes(5));
                    issuerPublicKey = DotNetUtilities.GetRsaPublicKey(issuer.GetRSAPublicKey());
                }

                if (chainCertificateRequest.ConfigureCrl)
                {
                    // for a certificate in a chain create CRL distribution point extension
                    var issuerDN = chainCertificateRequest?.Issuer?.Subject ?? subjectDN;
                    var crlServerUri = $"{chainCertificateRequest.CrlServerBaseUri}{issuerDN}.crl";
                    var generalName = new Org.BouncyCastle.Asn1.X509.GeneralName(Org.BouncyCastle.Asn1.X509.GeneralName.UniformResourceIdentifier, new DerIA5String(crlServerUri));
                    var distPointName = new DistributionPointName(new GeneralNames(generalName));
                    var distPoint = new DistributionPoint(distPointName, null, null);

                    certGen.Extensions.Add(
                        new X509Extension(
                            TestOids.CrlDistributionPoints,
                            new DerSequence(distPoint).GetDerEncoded(),
                            critical: false));
                }

                if (chainCertificateRequest.IsCA)
                {
                    // update key usage with CA cert sign and crl sign attributes
                    keyUsage |= X509KeyUsageFlags.CrlSign | X509KeyUsageFlags.KeyCertSign;
                    isCA = true;
                }
            }

            var padding = paddingMode.ToPadding();
            var request = new CertificateRequest(subjectDN, rsa, hashAlgorithm.ConvertToSystemSecurityHashAlgorithmName(), padding);

            certGen.NotAfter = notAfter ?? DateTime.UtcNow.Add(TimeSpan.FromMinutes(30));
            certGen.NotBefore = DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(30));

            var random = new Random();
            certGen.SerialNumber = random.Next();

            certGen.Extensions.Add(
                new X509SubjectKeyIdentifierExtension(request.PublicKey, critical: false));

            if (isSelfSigned)
            {
                issuerPublicKey = DotNetUtilities.GetRsaPublicKey(rsa);
                keyUsage |= X509KeyUsageFlags.KeyCertSign;
            }

            certGen.Extensions.Add(
                new X509Extension(
                    Oids.AuthorityKeyIdentifier,
                    new AuthorityKeyIdentifierStructure(issuerPublicKey).GetEncoded(),
                    critical: false));

            if (isCA)
            {
                // The RFC 5280 states that If the cA boolean is not asserted,
                // then the keyCertSign bit in the key usage extension MUST NOT be
                // asserted.
                certGen.Extensions.Add(
                    new X509BasicConstraintsExtension(certificateAuthority: true, hasPathLengthConstraint: false, pathLengthConstraint: 0, critical: true));
            }

            certGen.Extensions.Add(
                new X509KeyUsageExtension(keyUsage, critical: false));

            // Allow changes
            modifyGenerator?.Invoke(certGen);

            foreach (var extension in certGen.Extensions)
            {
                request.CertificateExtensions.Add(extension);
            }

            X509Certificate2 certResult;

            if (isSelfSigned)
            {
                certResult = request.CreateSelfSigned(certGen.NotBefore, certGen.NotAfter);
            }
            else
            {
                // BigInteger ToByteArray returns a little endian byte array and CertificateRequest.Create expects a big endian array for serial number
                var byteSerialNumber = certGen.SerialNumber.ToByteArray();
                Array.Reverse(byteSerialNumber);
                using (var temp = request.Create(issuer, certGen.NotBefore, certGen.NotAfter, byteSerialNumber))
                {
                    certResult = temp.CopyWithPrivateKey(rsa);
                }
            }

            return new X509Certificate2(certResult.Export(X509ContentType.Pkcs12), password: (string)null, keyStorageFlags: X509KeyStorageFlags.Exportable);
        }

        private static RSASignaturePadding ToPadding(this RSASignaturePaddingMode mode)
        {
            switch (mode)
            {
                case RSASignaturePaddingMode.Pkcs1: return RSASignaturePadding.Pkcs1;
                case RSASignaturePaddingMode.Pss: return RSASignaturePadding.Pss;
            }

            return null;
        }

        public static X509Certificate2 GenerateCertificate(
            string issuerName,
            string subjectName,
            RSA issuerAlgorithm,
            RSA algorithm)
        {
            var subjectDN = $"C=US,S=WA,L=Redmond,O=NuGet,CN={subjectName}";
            var issuerDN = new X500DistinguishedName($"C=US,S=WA,L=Redmond,O=NuGet,CN={issuerName}");

            var notAfter = DateTime.UtcNow.Add(TimeSpan.FromHours(1));
            var notBefore = DateTime.UtcNow.Subtract(TimeSpan.FromHours(1));

            var random = new Random();
            var serial = random.Next();
            var serialNumber = BitConverter.GetBytes(serial);
            Array.Reverse(serialNumber);

            var hashAlgorithm = System.Security.Cryptography.HashAlgorithmName.SHA256;
            var request = new CertificateRequest(subjectDN, algorithm, hashAlgorithm, RSASignaturePadding.Pkcs1);

            request.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(certificateAuthority: true, hasPathLengthConstraint: false, pathLengthConstraint: 0, critical: true));
            request.CertificateExtensions.Add(
                new X509SubjectKeyIdentifierExtension(request.PublicKey, critical: false));
            // The RFC 5280 states that If the cA boolean is not asserted,
            // then the keyCertSign bit in the key usage extension MUST NOT be
            // asserted.
            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyCertSign, critical: true));
            request.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(new OidCollection { new Oid(Oids.CodeSigningEku) }, critical: true));

            var generator = X509SignatureGenerator.CreateForRSA(issuerAlgorithm, RSASignaturePadding.Pkcs1);

            using (var temp = request.Create(issuerDN, generator, notBefore, notAfter, serialNumber))
            {
                var certResult = temp.CopyWithPrivateKey(algorithm);
                return new X509Certificate2(certResult.Export(X509ContentType.Pkcs12), password: (string)null, keyStorageFlags: X509KeyStorageFlags.Exportable);
            }
        }

        /// <summary>
        /// Create a self signed certificate.
        /// </summary>
        public static X509Certificate2 GenerateSelfIssuedCertificate(RSA algorithm, string subjectName = null, bool isCa = true)
        {
            if (string.IsNullOrEmpty(subjectName))
            {
                subjectName = $"NuGet Test Self-Issued Certificate ({Guid.NewGuid().ToString()})";
            }

            var subjectDN = new X500DistinguishedName($"C=US,S=WA,L=Redmond,O=NuGet,CN={subjectName}");

            var hashAlgorithm = System.Security.Cryptography.HashAlgorithmName.SHA256;
            var request = new CertificateRequest(subjectDN, algorithm, hashAlgorithm, RSASignaturePadding.Pkcs1);

            var keyUsages = X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyCertSign;

            request.CertificateExtensions.Add(
                new X509SubjectKeyIdentifierExtension(request.PublicKey, critical: false));

            if (isCa)
            {
                keyUsages |= X509KeyUsageFlags.CrlSign;

                var publicKey = DotNetUtilities.GetRsaPublicKey(algorithm);

                request.CertificateExtensions.Add(
                    new X509Extension(
                        Oids.AuthorityKeyIdentifier,
                        new AuthorityKeyIdentifierStructure(publicKey).GetEncoded(),
                        critical: false));

                // The RFC 5280 states that If the cA boolean is not asserted,
                // then the keyCertSign bit in the key usage extension MUST NOT be
                // asserted.
                request.CertificateExtensions.Add(
                    new X509BasicConstraintsExtension(certificateAuthority: isCa, hasPathLengthConstraint: false, pathLengthConstraint: 0, critical: false));
            }

            request.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(new OidCollection { new Oid(Oids.CodeSigningEku) }, critical: true));

            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(keyUsages, critical: true));

            var now = DateTime.UtcNow;
            var certResult = request.CreateSelfSigned(notBefore: now.Subtract(TimeSpan.FromHours(1)), notAfter: now.AddHours(1));

            return new X509Certificate2(certResult.Export(X509ContentType.Pkcs12), password: (string)null, keyStorageFlags: X509KeyStorageFlags.Exportable);
        }

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
            return new X509Certificate2(cert.Export(X509ContentType.Pfx, password), password, X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
        }

        public static TrustedTestCert<TestCertificate> GenerateTrustedTestCertificate(TestDirectory certDir)
        {
            var actionGenerator = CertificateModificationGeneratorForCodeSigningEkuCert;

            // Code Sign EKU needs trust to a root authority
            // Add the cert to Root CA list in LocalMachine as it does not prompt a dialog
            // This makes all the associated tests to require admin privilege
            return TestCertificate.Generate(actionGenerator).WithTrust(StoreName.Root, StoreLocation.LocalMachine, certDir);
        }

        public static TrustedTestCert<TestCertificate> GenerateTrustedTestCertificateExpired(TestDirectory certDir)
        {
            var actionGenerator = CertificateModificationGeneratorExpiredCert;

            // Code Sign EKU needs trust to a root authority
            // Add the cert to Root CA list in LocalMachine as it does not prompt a dialog
            // This makes all the associated tests to require admin privilege
            return TestCertificate.Generate(actionGenerator).WithTrust(StoreName.Root, StoreLocation.LocalMachine, certDir);
        }

        public static TrustedTestCert<TestCertificate> GenerateTrustedTestCertificateNotYetValid(TestDirectory certDir)
        {
            var actionGenerator = CertificateModificationGeneratorNotYetValidCert;

            // Code Sign EKU needs trust to a root authority
            // Add the cert to Root CA list in LocalMachine as it does not prompt a dialog
            // This makes all the associated tests to require admin privilege
            return TestCertificate.Generate(actionGenerator).WithTrust(StoreName.Root, StoreLocation.LocalMachine, certDir);
        }

        public static TrustedTestCert<TestCertificate> GenerateTrustedTestCertificateThatExpiresIn10Seconds(TestDirectory certDir)
        {
            var actionGenerator = CertificateModificationGeneratorExpireIn10Seconds;

            // Code Sign EKU needs trust to a root authority
            // Add the cert to Root CA list in LocalMachine as it does not prompt a dialog
            // This makes all the associated tests to require admin privilege
            return TestCertificate.Generate(actionGenerator).WithTrust(StoreName.Root, StoreLocation.LocalMachine, certDir);
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
                offlineRevocation = "The revocation function was unable to check revocation because the revocation server was offline";
            }
            else if (RuntimeEnvironmentHelper.IsMacOSX)
            {
                offlineRevocation = "An incomplete certificate revocation check occurred.";
            }
            else
            {
                offlineRevocation = "unable to get certificate CRL";
            }

            Assert.Contains(issues, issue =>
                issue.Code == NuGetLogCode.NU3018 &&
                issue.Level == logLevel &&
                issue.Message.Contains(offlineRevocation));
        }

        public static void AssertRevocationStatusUnknown(IEnumerable<ILogMessage> issues, LogLevel logLevel)
        {
            Assert.Contains(issues, issue =>
                issue.Code == NuGetLogCode.NU3018 &&
                issue.Level == logLevel &&
                issue.Message.Contains("The revocation function was unable to check revocation for the certificate"));
        }

        public static void AssertNotUntrustedRoot(IEnumerable<ILogMessage> issues, LogLevel logLevel)
        {
            string untrustedRoot;

            if (RuntimeEnvironmentHelper.IsWindows)
            {
                untrustedRoot = "A certificate chain processed, but terminated in a root certificate which is not trusted by the trust provider";
            }
            else if (RuntimeEnvironmentHelper.IsMacOSX)
            {
                untrustedRoot = "The trust policy was not trusted.";
            }
            else
            {
                untrustedRoot = "self signed certificate";
            }

            Assert.DoesNotContain(issues, issue =>
                issue.Code == NuGetLogCode.NU3018 &&
                issue.Level == logLevel &&
                issue.Message.Contains(untrustedRoot));
        }

        public static void AssertUntrustedRoot(IEnumerable<ILogMessage> issues, LogLevel logLevel)
        {
            string untrustedRoot;

            if (RuntimeEnvironmentHelper.IsWindows)
            {
                untrustedRoot = "A certificate chain processed, but terminated in a root certificate which is not trusted by the trust provider";
            }
            else if (RuntimeEnvironmentHelper.IsMacOSX)
            {
                untrustedRoot = "The trust policy was not trusted.";
            }
            else
            {
                untrustedRoot = "self signed certificate";
            }

            Assert.Contains(issues, issue =>
                issue.Code == NuGetLogCode.NU3018 &&
                issue.Level == logLevel &&
                issue.Message.Contains(untrustedRoot));
        }

        public static string AddSignatureLogPrefix(string log, PackageIdentity package, string source)
        {
            return $"{string.Format(_signatureLogPrefix, package.Id, package.Version, source)} {log}";
        }
    }
}