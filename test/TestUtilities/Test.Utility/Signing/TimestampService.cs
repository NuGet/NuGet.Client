// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Tsp;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.X509.Extension;
using Org.BouncyCastle.X509.Store;

namespace Test.Utility.Signing
{
    // https://tools.ietf.org/html/rfc3161
    public sealed class TimestampService : HttpResponder
    {
        private const string RequestContentType = "application/timestamp-query";
        private const string ResponseContentType = "application/timestamp-response";

        // "baseline-ts-policy" from RFC 3628 (https://tools.ietf.org/html/rfc3628#section-5.2)
        private const string BaselineTimeStampPolicy = "0.4.0.2023.1.1";

        private readonly AsymmetricCipherKeyPair _keyPair;
        private readonly HashSet<BigInteger> _serialNumbers;
        private BigInteger _nextSerialNumber;

        /// <summary>
        /// Gets this certificate authority's certificate.
        /// </summary>
        public X509Certificate Certificate { get; }

        /// <summary>
        /// Gets the base URI specific to this HTTP responder.
        /// </summary>
        public override Uri Url { get; }

        /// <summary>
        /// Gets the issuing certificate authority.
        /// </summary>
        public CertificateAuthority CertificateAuthority { get; }

        private TimestampService(
            CertificateAuthority certificateAuthority,
            X509Certificate certificate,
            AsymmetricCipherKeyPair keyPair,
            Uri uri)
        {
            CertificateAuthority = certificateAuthority;
            Certificate = certificate;
            _keyPair = keyPair;
            Url = uri;
            _serialNumbers = new HashSet<BigInteger>();
            _nextSerialNumber = BigInteger.One;
        }

        public static TimestampService Create(CertificateAuthority certificateAuthority)
        {
            if (certificateAuthority == null)
            {
                throw new ArgumentNullException(nameof(certificateAuthority));
            }

            var keyPair = CertificateUtilities.CreateKeyPair();
            var id = Guid.NewGuid().ToString();
            var subjectName = new X509Name($"C=US,ST=WA,L=Redmond,O=NuGet,CN=NuGet Test Timestamp Service ({id})");

            Action<X509V3CertificateGenerator> customizeCertificate = generator =>
            {
                generator.AddExtension(
                    X509Extensions.AuthorityInfoAccess,
                    critical: false,
                    extensionValue: new DerSequence(
                        new AccessDescription(AccessDescription.IdADOcsp,
                            new GeneralName(GeneralName.UniformResourceIdentifier, certificateAuthority.OcspResponderUri.OriginalString)),
                        new AccessDescription(AccessDescription.IdADCAIssuers,
                            new GeneralName(GeneralName.UniformResourceIdentifier, certificateAuthority.CertificateUri.OriginalString))));
                generator.AddExtension(
                    X509Extensions.AuthorityKeyIdentifier,
                    critical: false,
                    extensionValue: new AuthorityKeyIdentifierStructure(certificateAuthority.Certificate));
                generator.AddExtension(
                    X509Extensions.SubjectKeyIdentifier,
                    critical: false,
                    extensionValue: new SubjectKeyIdentifierStructure(keyPair.Public));
                generator.AddExtension(
                    X509Extensions.BasicConstraints,
                    critical: true,
                    extensionValue: new BasicConstraints(cA: false));
                generator.AddExtension(
                    X509Extensions.KeyUsage,
                    critical: true,
                    extensionValue: new KeyUsage(KeyUsage.DigitalSignature));
                generator.AddExtension(
                    X509Extensions.ExtendedKeyUsage,
                    critical: true,
                    extensionValue: ExtendedKeyUsage.GetInstance(new DerSequence(KeyPurposeID.IdKPTimeStamping)));
            };

            var certificate = certificateAuthority.IssueCertificate(keyPair.Public, subjectName, customizeCertificate);
            var uri = certificateAuthority.GenerateRandomUri();

            return new TimestampService(certificateAuthority, certificate, keyPair, uri);
        }

#if IS_DESKTOP
        public override void Respond(HttpListenerContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (!string.Equals(context.Request.ContentType, RequestContentType, StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = 400;

                return;
            }

            var bytes = ReadRequestBody(context.Request);
            var request = new TimeStampRequest(bytes);
            var tokenGenerator = new TimeStampTokenGenerator(_keyPair.Private, Certificate, TspAlgorithms.Sha256, BaselineTimeStampPolicy);

            if (request.CertReq)
            {
                var certificates = X509StoreFactory.Create(
                    "Certificate/Collection",
                    new X509CollectionStoreParameters(new[] { Certificate }));

                tokenGenerator.SetCertificates(certificates);
            }

            var responseGenerator = new TimeStampResponseGenerator(tokenGenerator, TspAlgorithms.Allowed);
            var response = responseGenerator.Generate(request, _nextSerialNumber, DateTime.UtcNow);

            _serialNumbers.Add(_nextSerialNumber);
            _nextSerialNumber = _nextSerialNumber.Add(BigInteger.One);

            context.Response.ContentType = ResponseContentType;

            WriteResponseBody(context.Response, response.GetEncoded());
        }
#endif
    }
}