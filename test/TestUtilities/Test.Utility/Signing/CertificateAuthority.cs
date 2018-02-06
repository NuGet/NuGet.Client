// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Ocsp;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Ocsp;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.X509.Extension;

namespace Test.Utility.Signing
{
    public sealed class CertificateAuthority : HttpResponder
    {
        private readonly Dictionary<BigInteger, X509Certificate> _issuedCertificates;
        private readonly Dictionary<BigInteger, RevocationInfo> _revokedCertificates;
        private readonly Lazy<OcspResponder> _ocspResponder;
        private BigInteger _nextSerialNumber;

        /// <summary>
        /// This base URI is shared amongst all HTTP responders hosted by the same web host instance.
        /// </summary>
        public Uri SharedUri { get; }

        public X509Certificate Certificate { get; }

        /// <summary>
        /// Gets the base URI specific to this HTTP responder.
        /// </summary>
        public override Uri Url { get; }

        public OcspResponder OcspResponder => _ocspResponder.Value;
        public CertificateAuthority Parent { get; }

        public Uri CertificateUri { get; }
        public Uri OcspResponderUri { get; }
        internal AsymmetricCipherKeyPair KeyPair { get; }

        private CertificateAuthority(
            X509Certificate certificate,
            AsymmetricCipherKeyPair keyPair,
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
            _nextSerialNumber = certificate.SerialNumber.Add(BigInteger.One);
            _issuedCertificates = new Dictionary<BigInteger, X509Certificate>();
            _revokedCertificates = new Dictionary<BigInteger, RevocationInfo>();
            _ocspResponder = new Lazy<OcspResponder>(() => new OcspResponder(this, OcspResponderUri));
        }

        public X509Certificate IssueCertificate(IssueCertificateOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var serialNumber = _nextSerialNumber;
            var issuerName = PrincipalUtilities.GetSubjectX509Principal(Certificate);
            Action<X509V3CertificateGenerator> customizeCertificate;

            if (options.CustomizeCertificate == null)
            {
                customizeCertificate = generator =>
                {
                    generator.AddExtension(
                        X509Extensions.AuthorityInfoAccess,
                        critical: false,
                        extensionValue: new DerSequence(
                            new AccessDescription(AccessDescription.IdADOcsp,
                                new GeneralName(GeneralName.UniformResourceIdentifier, OcspResponderUri.OriginalString)),
                            new AccessDescription(AccessDescription.IdADCAIssuers,
                                new GeneralName(GeneralName.UniformResourceIdentifier, CertificateUri.OriginalString))));
                    generator.AddExtension(
                        X509Extensions.AuthorityKeyIdentifier,
                        critical: false,
                        extensionValue: new AuthorityKeyIdentifierStructure(Certificate));
                    generator.AddExtension(
                        X509Extensions.SubjectKeyIdentifier,
                        critical: false,
                        extensionValue: new SubjectKeyIdentifierStructure(options.PublicKey));
                    generator.AddExtension(
                        X509Extensions.BasicConstraints,
                        critical: true,
                        extensionValue: new BasicConstraints(cA: false));
                };
            }
            else
            {
                customizeCertificate = options.CustomizeCertificate;
            }

            var notAfter = options.NotAfter.UtcDateTime;

            // An issued certificate should not have a validity period beyond the issuer's validity period.
            if (notAfter > Certificate.NotAfter)
            {
                notAfter = Certificate.NotAfter;
            }

            var certificate = CreateCertificate(
                options.PublicKey,
                KeyPair.Private,
                serialNumber,
                issuerName,
                options.SubjectName,
                options.NotBefore.UtcDateTime,
                notAfter,
                customizeCertificate);

            _nextSerialNumber = _nextSerialNumber.Add(BigInteger.One);
            _issuedCertificates.Add(certificate.SerialNumber, certificate);

            return certificate;
        }

        public CertificateAuthority CreateIntermediateCertificateAuthority()
        {
            var keyPair = CertificateUtilities.CreateKeyPair();
            var certificate = IssueCaCertificate(keyPair.Public);

            return new CertificateAuthority(certificate, keyPair, SharedUri, parentCa: this);
        }

        public void Revoke(X509Certificate certificate, int reason, DateTimeOffset revocationDate)
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

            _revokedCertificates.Add(certificate.SerialNumber, new RevocationInfo(certificate.SerialNumber, revocationDate.UtcDateTime, reason));
        }

#if IS_DESKTOP
        public override void Respond(HttpListenerContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (IsGet(context.Request) &&
                string.Equals(context.Request.RawUrl, CertificateUri.AbsolutePath, StringComparison.OrdinalIgnoreCase))
            {
                WriteResponseBody(context.Response, Certificate.GetEncoded());
            }
            else
            {
                context.Response.StatusCode = 404;
            }
        }
#endif

        public static CertificateAuthority Create(Uri sharedUri)
        {
            if (sharedUri == null)
            {
                throw new ArgumentNullException(nameof(sharedUri));
            }

            if (!sharedUri.AbsoluteUri.EndsWith("/"))
            {
                sharedUri = new Uri($"{sharedUri.AbsoluteUri}/");
            }

            var keyPair = CertificateUtilities.CreateKeyPair();
            var id = Guid.NewGuid().ToString();
            var subjectName = new X509Name($"C=US,ST=WA,L=Redmond,O=NuGet,CN=NuGet Test Root Certificate Authority ({id})");
            var now = DateTime.UtcNow;

            void customizeCertificate(X509V3CertificateGenerator generator)
            {
                generator.AddExtension(
                    X509Extensions.SubjectKeyIdentifier,
                    critical: false,
                    extensionValue: new SubjectKeyIdentifierStructure(keyPair.Public));
                generator.AddExtension(
                    X509Extensions.BasicConstraints,
                    critical: true,
                    extensionValue: new BasicConstraints(cA: true));
                generator.AddExtension(
                    X509Extensions.KeyUsage,
                    critical: true,
                    extensionValue: new KeyUsage(KeyUsage.DigitalSignature | KeyUsage.KeyCertSign | KeyUsage.CrlSign));
            }

            var certificate = CreateCertificate(
                keyPair.Public,
                keyPair.Private,
                BigInteger.One,
                subjectName,
                subjectName,
                now,
                now.AddHours(2),
                customizeCertificate);

            return new CertificateAuthority(certificate, keyPair, sharedUri, parentCa: null);
        }

        internal CertificateStatus GetStatus(CertificateID certificateId)
        {
            if (certificateId == null)
            {
                throw new ArgumentNullException(nameof(certificateId));
            }

            if (certificateId.MatchesIssuer(Certificate) &&
                _issuedCertificates.ContainsKey(certificateId.SerialNumber))
            {
                RevocationInfo revocationInfo;

                if (!_revokedCertificates.TryGetValue(certificateId.SerialNumber, out revocationInfo))
                {
                    return CertificateStatus.Good;
                }

                var revocationDate = new DerGeneralizedTime(revocationInfo.RevocationDate);
                var reason = new CrlReason(revocationInfo.Reason);
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

        private X509Certificate IssueCaCertificate(
            AsymmetricKeyParameter publicKey,
            Action<X509V3CertificateGenerator> customizeCertificate = null)
        {
            var id = Guid.NewGuid().ToString();
            var subjectName = new X509Name($"C=US,ST=WA,L=Redmond,O=NuGet,CN=NuGet Test Intermediate Certificate Authority ({id})");

            if (customizeCertificate == null)
            {
                customizeCertificate = generator =>
                {
                    generator.AddExtension(
                        X509Extensions.AuthorityInfoAccess,
                        critical: false,
                        extensionValue: new DerSequence(
                            new AccessDescription(AccessDescription.IdADOcsp,
                                new GeneralName(GeneralName.UniformResourceIdentifier, OcspResponderUri.OriginalString)),
                            new AccessDescription(AccessDescription.IdADCAIssuers,
                                new GeneralName(GeneralName.UniformResourceIdentifier, CertificateUri.OriginalString))));
                    generator.AddExtension(
                        X509Extensions.AuthorityKeyIdentifier,
                        critical: false,
                        extensionValue: new AuthorityKeyIdentifierStructure(Certificate));
                    generator.AddExtension(
                        X509Extensions.SubjectKeyIdentifier,
                        critical: false,
                        extensionValue: new SubjectKeyIdentifierStructure(publicKey));
                    generator.AddExtension(
                        X509Extensions.BasicConstraints,
                        critical: true,
                        extensionValue: new BasicConstraints(cA: true));
                };
            }

            var options = new IssueCertificateOptions(publicKey)
                {
                    SubjectName = subjectName,
                    CustomizeCertificate = customizeCertificate
                };

            return IssueCertificate(options);
        }

        private static X509Certificate CreateCertificate(
            AsymmetricKeyParameter certificatePublicKey,
            AsymmetricKeyParameter signingPrivateKey,
            BigInteger serialNumber,
            X509Name issuerName,
            X509Name subjectName,
            DateTime notBefore,
            DateTime notAfter,
            Action<X509V3CertificateGenerator> customizeCertificate)
        {
            var generator = new X509V3CertificateGenerator();

            generator.SetSerialNumber(serialNumber);
            generator.SetIssuerDN(issuerName);
            generator.SetNotBefore(notBefore);
            generator.SetNotAfter(notAfter);
            generator.SetSubjectDN(subjectName);
            generator.SetPublicKey(certificatePublicKey);

            customizeCertificate(generator);

            var signatureFactory = new Asn1SignatureFactory("SHA256WITHRSA", signingPrivateKey);

            return generator.Generate(signatureFactory);
        }

        private sealed class RevocationInfo
        {
            internal BigInteger SerialNumber { get; }
            internal DateTime RevocationDate { get; }
            internal int Reason { get; }

            internal RevocationInfo(BigInteger serialNumber, DateTime revocationDate, int reason)
            {
                SerialNumber = serialNumber;
                RevocationDate = revocationDate;
                Reason = reason;
            }
        }
    }
}