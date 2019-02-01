// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Numerics;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Ocsp;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Ocsp;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;
using X509Extension = Org.BouncyCastle.Asn1.X509.X509Extension;

namespace Test.Utility.Signing
{
    // https://tools.ietf.org/html/rfc6960
    public sealed class OcspResponder : HttpResponder
    {
        private const string RequestContentType = "application/ocsp-request";
        private const string ResponseContentType = "application/ocsp-response";

        private readonly OcspResponderOptions _options;
        private readonly ConcurrentDictionary<string, DateTimeOffset> _responses;

        public override Uri Url { get; }

        internal CertificateAuthority CertificateAuthority { get; }

        private OcspResponder(CertificateAuthority certificateAuthority, OcspResponderOptions options)
        {
            CertificateAuthority = certificateAuthority;
            Url = certificateAuthority.OcspResponderUri;
            _options = options;
            _responses = new ConcurrentDictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
        }

        public static OcspResponder Create(
            CertificateAuthority certificateAuthority,
            OcspResponderOptions options = null)
        {
            if (certificateAuthority == null)
            {
                throw new ArgumentNullException(nameof(certificateAuthority));
            }

            options = options ?? new OcspResponderOptions();

            return new OcspResponder(certificateAuthority, options);
        }

        public override void Respond(HttpListenerContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var bytes = GetOcspRequest(context);

            if (bytes == null)
            {
                context.Response.StatusCode = 400;

                return;
            }

            var ocspReq = new OcspReq(bytes);
            var pKey = CertificateAuthority.Certificate.GetRSAPublicKey();
            var respId = new RespID(DotNetUtilities.GetRsaPublicKey(pKey));
            var basicOcspRespGenerator = new BasicOcspRespGenerator(respId);
            var requests = ocspReq.GetRequestList();
            var nonce = ocspReq.GetExtensionValue(OcspObjectIdentifiers.PkixOcspNonce);

            if (nonce != null)
            {
                var extensions = new X509Extensions(new Dictionary<DerObjectIdentifier, X509Extension>()
                {
                    { OcspObjectIdentifiers.PkixOcspNonce, new X509Extension(critical: false, value: nonce) }
                });

                basicOcspRespGenerator.SetResponseExtensions(extensions);
            }

            var now = DateTimeOffset.UtcNow;

            foreach (var request in requests)
            {
                var certificateId = request.GetCertID();
                var certificateStatus = CertificateAuthority.GetStatus(certificateId);
                var thisUpdate = _options.ThisUpdate ?? now;
                var nextUpdate = _options.NextUpdate ?? now.AddSeconds(1);

                var serial = BigInteger.Parse(certificateId.SerialNumber.ToString()).ToString($"X{CertificateAuthority.Certificate.SerialNumber.Length}");
                _responses.AddOrUpdate(serial, nextUpdate, (key, currentNextUpdate) =>
                {
                    if (nextUpdate > currentNextUpdate)
                    {
                        return nextUpdate;
                    }

                    return currentNextUpdate;
                });

                basicOcspRespGenerator.AddResponse(certificateId, certificateStatus, thisUpdate.UtcDateTime, nextUpdate.UtcDateTime, singleExtensions: null);
            }

            var certificateChain = GetCertificateChain();

            var keypair = DotNetUtilities.GetRsaKeyPair(CertificateAuthority.KeyPair);
            var basicOcspResp = basicOcspRespGenerator.Generate("SHA256WITHRSA", keypair.Private, certificateChain, now.UtcDateTime);
            var ocspRespGenerator = new OCSPRespGenerator();
            var ocspResp = ocspRespGenerator.Generate(OCSPRespGenerator.Successful, basicOcspResp);

            bytes = ocspResp.GetEncoded();

            context.Response.ContentType = ResponseContentType;

            WriteResponseBody(context.Response, bytes);
        }

        private static byte[] GetOcspRequest(HttpListenerContext context)
        {
            // See https://tools.ietf.org/html/rfc6960#appendix-A.
            if (IsGet(context.Request))
            {
                var path = context.Request.RawUrl;
                var urlEncoded = path.Substring(path.IndexOf('/', 1)).TrimStart('/');
                var base64 = WebUtility.UrlDecode(urlEncoded);

                return Convert.FromBase64String(base64);
            }

            if (IsPost(context.Request) &&
                string.Equals(context.Request.ContentType, RequestContentType, StringComparison.OrdinalIgnoreCase))
            {
                return ReadRequestBody(context.Request);
            }

            return null;
        }

        public Task WaitForResponseExpirationAsync(X509Certificate2 certificate)
        {
            if (certificate == null)
            {
                throw new ArgumentNullException(nameof(certificate));
            }

            if (_responses.TryGetValue(certificate.SerialNumber, out var nextUpdate))
            {
                // Ensure expiration
                var delay = nextUpdate.AddSeconds(1) - DateTimeOffset.UtcNow;

                if (delay > TimeSpan.Zero)
                {
                    return Task.Delay(delay);
                }
            }

            return Task.CompletedTask;
        }

        private X509Certificate[] GetCertificateChain()
        {
            var certificates = new List<X509Certificate>();
            var certificateAuthority = CertificateAuthority;

            while (certificateAuthority != null)
            {
                var cert = DotNetUtilities.FromX509Certificate(certificateAuthority.Certificate);
                certificates.Add(cert);

                certificateAuthority = certificateAuthority.Parent;
            }

            return certificates.ToArray();
        }
    }
}