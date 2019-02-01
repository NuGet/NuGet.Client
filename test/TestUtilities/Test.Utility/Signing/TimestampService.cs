// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Cmp;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Tsp;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.X509.Extension;
using Org.BouncyCastle.X509.Store;
using GeneralName = Org.BouncyCastle.Asn1.X509.GeneralName;

namespace Test.Utility.Signing
{
    // https://tools.ietf.org/html/rfc3161
    public sealed class TimestampService : HttpResponder
    {
        private const string RequestContentType = "application/timestamp-query";
        private const string ResponseContentType = "application/timestamp-response";

        private readonly AsymmetricCipherKeyPair _keyPair;
        private readonly TimestampServiceOptions _options;
        private readonly HashSet<BigInteger> _serialNumbers;
        private BigInteger _nextSerialNumber;

        /// <summary>
        /// Gets this certificate authority's certificate.
        /// </summary>
        public X509Certificate2 Certificate { get; }

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
            X509Certificate2 certificate,
            AsymmetricCipherKeyPair keyPair,
            Uri uri,
            TimestampServiceOptions options)
        {
            CertificateAuthority = certificateAuthority;

            Certificate = certificate;
            _keyPair = keyPair;
            Url = uri;
            _serialNumbers = new HashSet<BigInteger>();
            _nextSerialNumber = BigInteger.One;
            _options = options;
        }

        public static TimestampService Create(
            CertificateAuthority certificateAuthority,
            TimestampServiceOptions serviceOptions = null,
            BCIssueCertificateOptions issueCertificateOptions = null)
        {
            if (certificateAuthority == null)
            {
                throw new ArgumentNullException(nameof(certificateAuthority));
            }

            serviceOptions = serviceOptions ?? new TimestampServiceOptions();

            if (issueCertificateOptions == null)
            {
                issueCertificateOptions = BCIssueCertificateOptions.CreateDefaultForTimestampService();
            }

            void customizeCertificate(X509V3CertificateGenerator generator)
            {
                generator.AddExtension(
                    X509Extensions.AuthorityInfoAccess,
                    critical: false,
                    extensionValue: new DerSequence(
                        new AccessDescription(AccessDescription.IdADOcsp,
                            new GeneralName(GeneralName.UniformResourceIdentifier, certificateAuthority.OcspResponderUri.OriginalString)),
                        new AccessDescription(AccessDescription.IdADCAIssuers,
                            new GeneralName(GeneralName.UniformResourceIdentifier, certificateAuthority.CertificateUri.OriginalString))));

                var bcCACert = DotNetUtilities.FromX509Certificate(certificateAuthority.Certificate);
                generator.AddExtension(
                    X509Extensions.AuthorityKeyIdentifier,
                    critical: false,
                    extensionValue: new AuthorityKeyIdentifierStructure(bcCACert));
                generator.AddExtension(
                    X509Extensions.SubjectKeyIdentifier,
                    critical: false,
                    extensionValue: new SubjectKeyIdentifierStructure(issueCertificateOptions.KeyPair.Public));
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
            }

            if (issueCertificateOptions.CustomizeCertificate == null)
            {
                issueCertificateOptions.CustomizeCertificate = customizeCertificate;
            }

            if (serviceOptions.IssuedCertificateNotBefore.HasValue)
            {
                issueCertificateOptions.NotBefore = serviceOptions.IssuedCertificateNotBefore.Value;
            }

            if (serviceOptions.IssuedCertificateNotAfter.HasValue)
            {
                issueCertificateOptions.NotAfter = serviceOptions.IssuedCertificateNotAfter.Value;
            }

            var certificate = certificateAuthority.IssueCertificateWithBC(issueCertificateOptions);

            var uri = certificateAuthority.GenerateRandomUri();

            return new TimestampService(certificateAuthority, certificate, issueCertificateOptions.KeyPair, uri, serviceOptions);
        }

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
            var bcCert = DotNetUtilities.FromX509Certificate(Certificate);

            var tokenGenerator = new TimeStampTokenGenerator(
                _keyPair.Private,
                bcCert,
                _options.SignatureHashAlgorithm.Value,
                _options.Policy.Value);

            if (_options.ReturnSigningCertificate)
            {
                var certificates = X509StoreFactory.Create(
                    "Certificate/Collection",
                    new X509CollectionStoreParameters(new[] { bcCert }));

                tokenGenerator.SetCertificates(certificates);
            }

            SetAccuracy(tokenGenerator);

            var responseGenerator = new TimeStampResponseGenerator(tokenGenerator, TspAlgorithms.Allowed);
            TimeStampResponse response;

            if (_options.ReturnFailure)
            {
                response = responseGenerator.GenerateFailResponse(
                    PkiStatus.Rejection,
                    PkiFailureInfo.BadAlg,
                    "Unsupported algorithm");
            }
            else
            {
                var generalizedTime = DateTime.UtcNow;

                if (_options.GeneralizedTime.HasValue)
                {
                    generalizedTime = _options.GeneralizedTime.Value.UtcDateTime;
                }
                response = responseGenerator.Generate(request, _nextSerialNumber, generalizedTime);
            }

            _serialNumbers.Add(_nextSerialNumber);
            _nextSerialNumber = _nextSerialNumber.Add(BigInteger.One);

            context.Response.ContentType = ResponseContentType;

            WriteResponseBody(context.Response, response.GetEncoded());
        }

        private void SetAccuracy(TimeStampTokenGenerator tokenGenerator)
        {
            if (_options.Accuracy != null)
            {
                if (_options.Accuracy.Seconds != null)
                {
                    tokenGenerator.SetAccuracySeconds(_options.Accuracy.Seconds.Value.IntValue);
                }

                if (_options.Accuracy.Millis != null)
                {
                    tokenGenerator.SetAccuracyMillis(_options.Accuracy.Millis.Value.IntValue);
                }

                if (_options.Accuracy.Micros != null)
                {
                    tokenGenerator.SetAccuracyMicros(_options.Accuracy.Micros.Value.IntValue);
                }
            }
        }
    }
}