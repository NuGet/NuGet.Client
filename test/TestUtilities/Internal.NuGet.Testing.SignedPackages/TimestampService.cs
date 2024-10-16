// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable
#pragma warning disable CS1591

using System;
using System.Collections.Generic;
#if IS_SIGNING_SUPPORTED
using System.Formats.Asn1;
using System.Net;
#endif
using System.Numerics;
using System.Security.Cryptography;
#if IS_SIGNING_SUPPORTED
using System.Security.Cryptography.Pkcs;
#endif
using System.Security.Cryptography.X509Certificates;
using NuGet.Packaging.Signing;
#if IS_SIGNING_SUPPORTED
using TestTstInfo = Test.Utility.Signing.TstInfo;
#endif

namespace Test.Utility.Signing
{
    // https://tools.ietf.org/html/rfc3161
    public sealed class TimestampService : HttpResponder
    {
        private const string RequestContentType = "application/timestamp-query";
        private const string ResponseContentType = "application/timestamp-response";

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
            Uri uri,
            TimestampServiceOptions options)
        {
            CertificateAuthority = certificateAuthority;
            Certificate = certificate;
            Url = uri;
            _serialNumbers = new HashSet<BigInteger>();
            _nextSerialNumber = BigInteger.One;
            _options = options;
        }

        public static TimestampService Create(
            CertificateAuthority certificateAuthority,
            TimestampServiceOptions? serviceOptions = null,
            IssueCertificateOptions? issueCertificateOptions = null)
        {
            if (certificateAuthority == null)
            {
                throw new ArgumentNullException(nameof(certificateAuthority));
            }

            serviceOptions = serviceOptions ?? new TimestampServiceOptions();

            if (issueCertificateOptions == null)
            {
                issueCertificateOptions = IssueCertificateOptions.CreateDefaultForTimestampService();
            }

            void customizeCertificate(CertificateRequest certificateRequest)
            {
                certificateRequest.CertificateExtensions.Add(
                    new X509AuthorityInformationAccessExtension(
                        certificateAuthority.OcspResponderUri,
                        certificateAuthority.CertificateUri));
                certificateRequest.CertificateExtensions.Add(
                    X509AuthorityKeyIdentifierExtension.CreateFromCertificate(
                        certificateAuthority.Certificate,
                        includeKeyIdentifier: true,
                        includeIssuerAndSerial: true));
                certificateRequest.CertificateExtensions.Add(
                    new X509SubjectKeyIdentifierExtension(certificateRequest.PublicKey, critical: false));
                certificateRequest.CertificateExtensions.Add(
                    new X509BasicConstraintsExtension(
                        certificateAuthority: false,
                        hasPathLengthConstraint: false,
                        pathLengthConstraint: 0,
                        critical: true));
                certificateRequest.CertificateExtensions.Add(
                    new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, critical: true));
                certificateRequest.CertificateExtensions.Add(
                    new X509EnhancedKeyUsageExtension(
                        new OidCollection() { new Oid(Oids.TimeStampingEku) },
                        critical: true));
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

            X509Certificate2 certificate = certificateAuthority.IssueCertificate(issueCertificateOptions);
            Uri uri = certificateAuthority.GenerateRandomUri();

            return new TimestampService(certificateAuthority, certificate, uri, serviceOptions);
        }

#if IS_SIGNING_SUPPORTED
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

            byte[] bytes = ReadRequestBody(context.Request);
            TimeStampReq request = TimeStampReq.Decode(bytes);
            PkiStatusInfo statusInfo;
            SignedCms? timestamp = null;

            if (_options.ReturnFailure)
            {
                statusInfo = new PkiStatusInfo(PkiStatus.Rejection, "Unsupported algorithm", PkiFailureInfo.BadAlg);
            }
            else
            {
                statusInfo = new PkiStatusInfo(PkiStatus.Granted);

                var generalizedTime = DateTime.UtcNow;

                if (_options.GeneralizedTime.HasValue)
                {
                    generalizedTime = _options.GeneralizedTime.Value.UtcDateTime;
                }

                timestamp = GenerateTimestamp(request, _nextSerialNumber, generalizedTime);
            }

            _serialNumbers.Add(_nextSerialNumber);
            _nextSerialNumber += BigInteger.One;

            context.Response.ContentType = ResponseContentType;

            var response = new TimeStampResp(statusInfo, timestamp);
            ReadOnlyMemory<byte> encodedResponse = response.Encode();

            WriteResponseBody(context.Response, encodedResponse);
        }

        private SignedCms GenerateTimestamp(
            TimeStampReq request,
            BigInteger serialNumber,
            DateTime generalizedTime)
        {
            AsnWriter writer = new(AsnEncodingRules.DER);
            Accuracy? accuracy = FromTimeSpan(_options.Accuracy);
            TestTstInfo tstInfo = new(
                BigInteger.One,
                _options.Policy,
                request.MessageImprint,
                serialNumber,
                generalizedTime,
                accuracy,
                nonce: request.Nonce);

            tstInfo.Encode(writer, omitFractionalSeconds: true);

            byte[] encodedTstInfo = writer.Encode();
            ContentInfo contentInfo = new(new Oid(Oids.TSTInfoContentType), encodedTstInfo);
            SignedCms signedCms = new(contentInfo);
            CmsSigner cmsSigner;

            if (Certificate.Extensions[Oids.SubjectKeyIdentifier] is null)
            {
                cmsSigner = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, Certificate);
            }
            else
            {
                cmsSigner = new CmsSigner(SubjectIdentifierType.SubjectKeyIdentifier, Certificate);
            }

            var certificateBytes = new Lazy<byte[]>(() => Certificate.GetRawCertData());

            if (_options.SigningCertificateUsage.HasFlag(SigningCertificateUsage.V1))
            {
                SigningCertificate signingCertificate = SigningCertificate.Create(_options.SigningCertificateV1Hash ?? Certificate.GetCertHash(), Certificate);

                writer.Reset();
                signingCertificate.Encode(writer);

                cmsSigner.SignedAttributes.Add(new AsnEncodedData(Oids.SigningCertificate, writer.Encode()));
            }

            if (_options.SigningCertificateUsage.HasFlag(SigningCertificateUsage.V2))
            {
                SigningCertificateV2 signingCertificate = SigningCertificateV2.Create(HashAlgorithmName.SHA256, Certificate);

                writer.Reset();
                signingCertificate.Encode(writer);

                byte[] bytes = writer.Encode();

                cmsSigner.SignedAttributes.Add(new AsnEncodedData(Oids.SigningCertificateV2, bytes));
            }

            if (_options.ReturnSigningCertificate)
            {
                cmsSigner.IncludeOption = X509IncludeOption.EndCertOnly;
            }
            else
            {
                cmsSigner.IncludeOption = X509IncludeOption.None;
            }

            cmsSigner.DigestAlgorithm = _options.SignatureHashAlgorithm;

            signedCms.ComputeSignature(cmsSigner);

            return signedCms;
        }

        private static Accuracy? FromTimeSpan(TimeSpan? timespan)
        {
            if (timespan is null)
            {
                return null;
            }

            int? seconds = (int)timespan.Value.TotalSeconds;
            int? milliseconds = timespan.Value.Milliseconds;
            int? microseconds = (int)((double)(timespan.Value.Ticks % 10000) * 0.1);

            if (microseconds == 0)
            {
                microseconds = null;

                if (milliseconds == 0)
                {
                    milliseconds = null;
                }
            }

            return new Accuracy(seconds, milliseconds, microseconds);
        }
#endif
    }
}
