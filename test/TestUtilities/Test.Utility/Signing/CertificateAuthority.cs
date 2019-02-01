// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using NuGet.Packaging.Signing;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Ocsp;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509.Extension;
using X509Extension = System.Security.Cryptography.X509Certificates.X509Extension;
using Org.BouncyCastle.Asn1.Ocsp;
using NuGet.Common;
using GeneralName = Org.BouncyCastle.Asn1.X509.GeneralName;
using HashAlgorithmName = System.Security.Cryptography.HashAlgorithmName;
using System.Numerics;
using System.Globalization;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto;

namespace Test.Utility.Signing
{
    public sealed class CertificateAuthority : HttpResponder
    {
        private readonly Dictionary<string, X509Certificate2> _issuedCertificates;
        private readonly Dictionary<string, RevocationInfo> _revokedCertificates;
        private readonly Lazy<OcspResponder> _ocspResponder;
        private BigInteger _nextSerialNumber;

        /// <summary>
        /// This base URI is shared amongst all HTTP responders hosted by the same web host instance.
        /// </summary>
        public Uri SharedUri { get; }

        public X509Certificate2 Certificate { get; }

        /// <summary>
        /// Gets the base URI specific to this HTTP responder.
        /// </summary>
        public override Uri Url { get; }

        public OcspResponder OcspResponder => _ocspResponder.Value;

        public CertificateAuthority Parent { get; }

        public Uri CertificateUri { get; }

        public Uri OcspResponderUri { get; }

        internal RSA KeyPair { get; }

        private CertificateAuthority(
            X509Certificate2 certificate,
            RSA keyPair,
            Uri sharedUri,
            CertificateAuthority parentCa)
        {
            Certificate = certificate;
            KeyPair = keyPair;
            SharedUri = sharedUri;
            Url = GenerateRandomUri();
            var fingerprint = CertificateUtilities.GenerateFingerprint(certificate);
            CertificateUri = new Uri(Url, $"{fingerprint}.cer");
            OcspResponderUri = GenerateRandomUri();
            Parent = parentCa;
            var cerSerial = BigInteger.Parse(certificate.SerialNumber, NumberStyles.HexNumber);
            _nextSerialNumber = BigInteger.Add(cerSerial, BigInteger.One);
            _issuedCertificates = new Dictionary<string, X509Certificate2>();
            _revokedCertificates = new Dictionary<string, RevocationInfo>();
            _ocspResponder = new Lazy<OcspResponder>(() => OcspResponder.Create(this));
        }

        public X509Certificate2 IssueCertificate(IssueCertificateOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var signatureGenerator = X509SignatureGenerator.CreateForRSA(options.KeyPair, RSASignaturePadding.Pkcs1);

            void customizeCertificate(TestCertificateGenerator generator)
            {
                generator.Extensions.Add(
                    new X509Extension(
                        TestOids.AuthorityInfoAccess,
                        new DerSequence(
                            new AccessDescription(AccessDescription.IdADOcsp,
                                new GeneralName(GeneralName.UniformResourceIdentifier, OcspResponderUri.OriginalString)),
                            new AccessDescription(AccessDescription.IdADCAIssuers,
                                new GeneralName(GeneralName.UniformResourceIdentifier, CertificateUri.OriginalString))).GetDerEncoded(),
                        critical: false));

                var publicKey = DotNetUtilities.GetRsaPublicKey(Certificate.GetRSAPublicKey());

                generator.Extensions.Add(
                    new X509Extension(
                        Oids.AuthorityKeyIdentifier,
                        new AuthorityKeyIdentifierStructure(publicKey).GetEncoded(),
                        critical: false));
                generator.Extensions.Add(
                    new X509SubjectKeyIdentifierExtension(signatureGenerator.PublicKey, critical: false));
                generator.Extensions.Add(
                    new X509BasicConstraintsExtension(certificateAuthority: false, hasPathLengthConstraint: false, pathLengthConstraint: 0, critical: true));

            }

            return IssueCertificate(options, customizeCertificate);
        }

        public X509Certificate2 IssueCertificateWithBC(BCIssueCertificateOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            void customizeCertificate(X509V3CertificateGenerator generator)
            {
                generator.AddExtension(
                    X509Extensions.AuthorityInfoAccess,
                    critical: false,
                    extensionValue: new DerSequence(
                        new AccessDescription(AccessDescription.IdADOcsp,
                            new GeneralName(GeneralName.UniformResourceIdentifier, OcspResponderUri.OriginalString)),
                        new AccessDescription(AccessDescription.IdADCAIssuers,
                            new GeneralName(GeneralName.UniformResourceIdentifier, CertificateUri.OriginalString))));

                var bcCert = DotNetUtilities.FromX509Certificate(Certificate);

                generator.AddExtension(
                    X509Extensions.AuthorityKeyIdentifier,
                    critical: false,
                    extensionValue: new AuthorityKeyIdentifierStructure(bcCert));
                generator.AddExtension(
                    X509Extensions.SubjectKeyIdentifier,
                    critical: false,
                    extensionValue: new SubjectKeyIdentifierStructure(options.KeyPair.Public));
                generator.AddExtension(
                    X509Extensions.BasicConstraints,
                    critical: true,
                    extensionValue: new BasicConstraints(cA: false));
            }

            return IssueCertificateWithBC(options, customizeCertificate);
        }

        public CertificateAuthority CreateIntermediateCertificateAuthority(IssueCertificateOptions options = null)
        {
            options = options ?? IssueCertificateOptions.CreateDefaultForIntermediateCertificateAuthority();

            var signatureGenerator = X509SignatureGenerator.CreateForRSA(options.KeyPair, RSASignaturePadding.Pkcs1);

            void customizeCertificate(TestCertificateGenerator generator)
            {
                generator.Extensions.Add(
                    new X509Extension(
                        TestOids.AuthorityInfoAccess,
                        new DerSequence(
                            new AccessDescription(AccessDescription.IdADOcsp,
                                new GeneralName(GeneralName.UniformResourceIdentifier, OcspResponderUri.OriginalString)),
                            new AccessDescription(AccessDescription.IdADCAIssuers,
                                new GeneralName(GeneralName.UniformResourceIdentifier, CertificateUri.OriginalString))).GetDerEncoded(),
                        critical: false));

                var publicKey = DotNetUtilities.GetRsaPublicKey(Certificate.GetRSAPublicKey());

                generator.Extensions.Add(
                    new X509Extension(
                        Oids.AuthorityKeyIdentifier,
                        new AuthorityKeyIdentifierStructure(publicKey).GetEncoded(),
                        critical: false));
                generator.Extensions.Add(
                    new X509SubjectKeyIdentifierExtension(signatureGenerator.PublicKey, critical: false));
                generator.Extensions.Add(
                    new X509BasicConstraintsExtension(certificateAuthority: true, hasPathLengthConstraint: false, pathLengthConstraint: 0, critical: true));
            }

            var certificate = IssueCertificate(options, customizeCertificate);

            return new CertificateAuthority(certificate, options.KeyPair, SharedUri, parentCa: this);
        }

        public void Revoke(X509Certificate2 certificate, RevocationReason reason, DateTimeOffset revocationDate)
        {
            if (certificate == null)
            {
                throw new ArgumentNullException(nameof(certificate));
            }

            if (!_issuedCertificates.ContainsKey(certificate.SerialNumber))
            {
                throw new ArgumentException("Unknown serial number.", nameof(certificate));
            }

            if (_revokedCertificates.ContainsKey(certificate.SerialNumber))
            {
                throw new ArgumentException("Certificate already revoked.", nameof(certificate));
            }

            _revokedCertificates.Add(
                certificate.SerialNumber,
                new RevocationInfo(certificate.SerialNumber, revocationDate, reason));
        }

        public override void Respond(HttpListenerContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (IsGet(context.Request) &&
                string.Equals(context.Request.RawUrl, CertificateUri.AbsolutePath, StringComparison.OrdinalIgnoreCase))
            {
                var bcCert = DotNetUtilities.FromX509Certificate(Certificate);
                WriteResponseBody(context.Response, bcCert.GetEncoded());
            }
            else
            {
                context.Response.StatusCode = 404;
            }
        }

        public static CertificateAuthority Create(Uri sharedUri, IssueCertificateOptions options = null)
        {
            if (sharedUri == null)
            {
                throw new ArgumentNullException(nameof(sharedUri));
            }

            if (!sharedUri.AbsoluteUri.EndsWith("/"))
            {
                sharedUri = new Uri($"{sharedUri.AbsoluteUri}/");
            }

            options = options ?? IssueCertificateOptions.CreateDefaultForRootCertificateAuthority();

            var signatureGenerator = X509SignatureGenerator.CreateForRSA(options.KeyPair, RSASignaturePadding.Pkcs1);

            void customizeCertificate(TestCertificateGenerator generator)
            {
                generator.Extensions.Add(
                    new X509SubjectKeyIdentifierExtension(signatureGenerator.PublicKey, critical: false));
                generator.Extensions.Add(
                    new X509BasicConstraintsExtension(certificateAuthority: true, hasPathLengthConstraint: false, pathLengthConstraint: 0, critical: true));
                generator.Extensions.Add(
                    new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, critical: true));
            }

            var certificate = CreateCertificate(
                options.KeyPair,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1,
                BigInteger.One,
                options.SubjectName,
                options.NotBefore,
                options.NotAfter,
                options.CustomizeCertificate ?? customizeCertificate);

            return new CertificateAuthority(certificate, options.KeyPair, sharedUri, parentCa: null);
        }

        internal CertificateStatus GetStatus(CertificateID certificateId)
        {
            if (certificateId == null)
            {
                throw new ArgumentNullException(nameof(certificateId));
            }

            var bcCert = DotNetUtilities.FromX509Certificate(Certificate);
            // Make sure the serial has the same lenght and format as the current cert serial
            var serial = BigInteger.Parse(certificateId.SerialNumber.ToString()).ToString($"X{Certificate.SerialNumber.Length}");

            if (certificateId.MatchesIssuer(bcCert) &&
                _issuedCertificates.ContainsKey(serial))
            {
                RevocationInfo revocationInfo;

                if (!_revokedCertificates.TryGetValue(serial, out revocationInfo))
                {
                    return CertificateStatus.Good;
                }

                var datetimeString = DerGeneralizedTimeUtility.ToDerGeneralizedTimeString(revocationInfo.RevocationDate);

                // The DateTime constructor truncates fractional seconds;
                // however, the string constructor preserves full accuracy.
                var revocationDate = new DerGeneralizedTime(datetimeString);
                var reason = new CrlReason((int)revocationInfo.Reason);
                var revokedInfo = new RevokedInfo(revocationDate, reason);

                return new RevokedStatus(revokedInfo);
            }

            return new UnknownStatus();
        }

        internal Uri GenerateRandomUri()
        {
            using (var provider = RandomNumberGenerator.Create())
            {
                var bytes = new byte[32];

                provider.GetBytes(bytes);

                var path = BitConverter.ToString(bytes).Replace("-", "");

                return new Uri(SharedUri, $"{path}/");
            }
        }

        private X509Certificate2 IssueCertificate(
            IssueCertificateOptions options,
            Action<TestCertificateGenerator> customizeCertificate)
        {
            var serialNumber = _nextSerialNumber;

            var certificate = CreateCertificate(
                options.KeyPair,
                options.SignatureAlgorithmName,
                RSASignaturePadding.Pkcs1,
                serialNumber,
                options.SubjectName,
                options.NotBefore.UtcDateTime,
                options.NotAfter.UtcDateTime,
                options.CustomizeCertificate ?? customizeCertificate,
                Certificate);

            _nextSerialNumber = BigInteger.Add(_nextSerialNumber, BigInteger.One);
            _issuedCertificates.Add(certificate.SerialNumber, certificate);

            return certificate;
        }

        private X509Certificate2 IssueCertificateWithBC(
            BCIssueCertificateOptions options,
            Action<X509V3CertificateGenerator> customizeCertificate)
        {
            var serialNumber = _nextSerialNumber;
            var serialBytes = serialNumber.ToByteArray();
            Array.Reverse(serialBytes);
            var bcSerial = new Org.BouncyCastle.Math.BigInteger(serialBytes);
            var bcCert = DotNetUtilities.FromX509Certificate(Certificate);
            var issuerName = PrincipalUtilities.GetSubjectX509Principal(bcCert);
            var notAfter = options.NotAfter.UtcDateTime;

            // An issued certificate should not have a validity period beyond the issuer's validity period.
            if (notAfter > Certificate.NotAfter)
            {
                notAfter = Certificate.NotAfter;
            }

            var signatureFactory = new Asn1SignatureFactory(options.SignatureAlgorithmName, options.IssuerPrivateKey ?? DotNetUtilities.GetRsaKeyPair(KeyPair).Private);

            var certificate = CreateCertificateWithBC(
                options.KeyPair.Public,
                signatureFactory,
                bcSerial,
                issuerName,
                options.SubjectName,
                options.NotBefore.UtcDateTime,
                notAfter,
                options.CustomizeCertificate ?? customizeCertificate);

            _nextSerialNumber = BigInteger.Add(_nextSerialNumber, BigInteger.One);
            _issuedCertificates.Add(certificate.SerialNumber, certificate);

            return certificate;
        }

        private static X509Certificate2 CreateCertificateWithBC(
              AsymmetricKeyParameter certificatePublicKey,
              Asn1SignatureFactory signatureFactory,
              Org.BouncyCastle.Math.BigInteger serialNumber,
              X509Name issuerName,
              X509Name subjectName,
              DateTimeOffset notBefore,
              DateTimeOffset notAfter,
              Action<X509V3CertificateGenerator> customizeCertificate)
        {
            var generator = new X509V3CertificateGenerator();

            generator.SetSerialNumber(serialNumber);
            generator.SetIssuerDN(issuerName);
            generator.SetNotBefore(notBefore.UtcDateTime);
            generator.SetNotAfter(notAfter.UtcDateTime);
            generator.SetSubjectDN(subjectName);
            generator.SetPublicKey(certificatePublicKey);

            customizeCertificate(generator);

            var temp = generator.Generate(signatureFactory);

            var certResult = new X509Certificate2(temp.GetEncoded());
            return new X509Certificate2(certResult.Export(X509ContentType.Pkcs12), password: (string)null, keyStorageFlags: X509KeyStorageFlags.Exportable);
        }

        private static X509Certificate2 CreateCertificate(
            RSA certificateKey,
            HashAlgorithmName hashAlgorithm,
            RSASignaturePadding padding,
            BigInteger serialNumber,
            X500DistinguishedName subjectName,
            DateTimeOffset notBefore,
            DateTimeOffset notAfter,
            Action<TestCertificateGenerator> customizeCertificate,
            X509Certificate2 issuer = null)
        {
            var request = new CertificateRequest(subjectName, certificateKey, hashAlgorithm, padding);

            var generator = new TestCertificateGenerator
            {
                SerialNumber = serialNumber,
                NotBefore = notBefore.UtcDateTime,
                NotAfter = notAfter.UtcDateTime
            };

            customizeCertificate(generator);

            foreach (var extension in generator.Extensions)
            {
                request.CertificateExtensions.Add(extension);
            }

            X509Certificate2 certResult;

            if (issuer == null)
            {
                certResult = request.CreateSelfSigned(generator.NotBefore, generator.NotAfter);
            }
            else
            {
                var certNotBefore = generator.NotBefore;
                var certNotAfter = generator.NotAfter;

                // An issued certificate should not have a validity period beyond the issuer's validity period.
                if (certNotBefore < issuer.NotBefore)
                {
                    certNotBefore = issuer.NotBefore;
                }

                if (certNotAfter > issuer.NotAfter)
                {
                    certNotAfter = issuer.NotAfter;
                }

                // BigInteger ToByteArray returns a little endian byte array and CertificateRequest.Create expects a big endian array for serial number
                var byteSerialNumber = generator.SerialNumber.ToByteArray();
                Array.Reverse(byteSerialNumber);
                using (var temp = request.Create(issuer, certNotBefore, certNotAfter, byteSerialNumber))
                {
                    certResult = temp.CopyWithPrivateKey(certificateKey);
                }
            }

            return new X509Certificate2(certResult.Export(X509ContentType.Pkcs12), password: (string)null, keyStorageFlags: X509KeyStorageFlags.Exportable);
        }

        private sealed class RevocationInfo
        {
            internal string SerialNumber { get; }
            internal DateTimeOffset RevocationDate { get; }
            internal RevocationReason Reason { get; }

            internal RevocationInfo(string serialNumber, DateTimeOffset revocationDate, RevocationReason reason)
            {
                SerialNumber = serialNumber;
                RevocationDate = revocationDate;
                Reason = reason;
            }
        }
    }
}