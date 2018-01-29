// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Ocsp;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Ocsp;
using Org.BouncyCastle.X509;

namespace Test.Utility.Signing
{
    // https://tools.ietf.org/html/rfc6960
    public sealed class OcspResponder : HttpResponder
    {
        private const string RequestContentType = "application/ocsp-request";
        private const string ResponseContentType = "application/ocsp-response";

        public override Uri Url { get; }

        internal CertificateAuthority CertificateAuthority { get; }

        internal OcspResponder(CertificateAuthority certificateAuthority, Uri uri)
        {
            if (certificateAuthority == null)
            {
                throw new ArgumentNullException(nameof(certificateAuthority));
            }

            if (uri == null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            CertificateAuthority = certificateAuthority;
            Url = uri;
        }

#if IS_DESKTOP
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
            var respId = new RespID(CertificateAuthority.Certificate.SubjectDN);
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

            var now = DateTime.UtcNow;

            foreach (var request in requests)
            {
                var certificateId = request.GetCertID();
                var certificateStatus = CertificateAuthority.GetStatus(certificateId);

                basicOcspRespGenerator.AddResponse(certificateId, certificateStatus, thisUpdate: now, nextUpdate: now.AddSeconds(1), singleExtensions: null);
            }

            var certificateChain = GetCertificateChain();
            var basicOcspResp = basicOcspRespGenerator.Generate("SHA256WITHRSA", CertificateAuthority.KeyPair.Private, certificateChain, now);
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
#endif

        private X509Certificate[] GetCertificateChain()
        {
            var certificates = new List<X509Certificate>();
            var certificateAuthority = CertificateAuthority;

            while (certificateAuthority != null)
            {
                certificates.Add(certificateAuthority.Certificate);

                certificateAuthority = certificateAuthority.Parent;
            }

            return certificates.ToArray();
        }
    }
}