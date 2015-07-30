// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
#if !DNXCORE50
using System.IdentityModel.Protocols.WSTrust;
using System.IdentityModel.Tokens;
using System.Linq;
#endif
using System.Net;
using System.Net.Http;
#if !DNXCORE50
using System.ServiceModel;
using System.ServiceModel.Security;
#endif
using System.Text;
using NuGet.Configuration;

namespace NuGet.Protocol.Core.v3
{
    public class STSAuthHelper
    {
        /// <summary>
        /// Response header that specifies the WSTrust13 Windows Transport endpoint.
        /// </summary>
        private const string STSEndPointHeader = "X-NuGet-STS-EndPoint";

        /// <summary>
        /// Response header that specifies the realm to authenticate for. In most cases this would be the gallery we are going up against.
        /// </summary>
        private const string STSRealmHeader = "X-NuGet-STS-Realm";

        /// <summary>
        /// Request header that contains the SAML token.
        /// </summary>
        private const string STSTokenHeader = "X-NuGet-STS-Token";

        /// <summary>
        /// Adds the SAML token as a header to the request if it is already cached for this host.
        /// </summary>
        public static void PrepareSTSRequest(
            Uri feedUri,
            CredentialStore credentialStore,
            HttpRequestMessage request)
        {
#if !DNXCORE50
            var credentials = credentialStore.GetCredentials(feedUri) as STSCredentials;

            if (credentials != null)
            {
                request.Headers.TryAddWithoutValidation(STSTokenHeader, credentials.STSToken);
            }
#endif
        }

        /// <summary>
        /// Attempts to retrieve a SAML token if the response indicates that server requires STS-based auth. 
        /// </summary>
        public static bool TryRetrieveSTSToken(
            Uri feedUri,
            CredentialStore credentialStore,
            HttpResponseMessage response)
        {
#if DNXCORE50
            return false;
#else
            if (response.StatusCode != HttpStatusCode.Unauthorized)
            {
                // We only care to do STS auth if the server returned a 401
                return false;
            }

            var endPoint = GetHeader(response, STSEndPointHeader);
            var realm = GetHeader(response, STSRealmHeader);
            if (string.IsNullOrEmpty(endPoint) || string.IsNullOrEmpty(realm))
            {
                // The server does not conform to our STS-auth requirements. 
                return false;
            }

            var credentials = credentialStore.GetCredentials(feedUri) as STSCredentials;

            if (credentials == null)
            {
                var stsToken = GetSTSToken(feedUri, endPoint, realm);
                if (stsToken != null)
                {
                    stsToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(stsToken));
                    credentials = new STSCredentials(stsToken);
                    credentialStore.Add(feedUri, credentials);
                    return true;
                }
            }

            return false;
        }

        private static string GetSTSToken(Uri requestUri, string endPoint, string realm)
        {
            var binding = new WS2007HttpBinding(SecurityMode.Transport);
            var factory = new WSTrustChannelFactory(binding, endPoint)
            {
                TrustVersion = TrustVersion.WSTrust13
            };

            var endPointReference = new EndpointReference(realm);
            var requestToken = new RequestSecurityToken
            {
                RequestType = RequestTypes.Issue,
                KeyType = KeyTypes.Bearer,
                AppliesTo = endPointReference
            };

            var channel = factory.CreateChannel();
            var responseToken = channel.Issue(requestToken) as GenericXmlSecurityToken;
            return responseToken?.TokenXml.OuterXml;
        }

        private static string GetHeader(HttpResponseMessage response, string header)
        {
            IEnumerable<string> values;
            if (response.Headers.TryGetValues(header, out values))
            {
                return values.FirstOrDefault();
            }

            return null;
        }

        private class STSCredentials : ICredentials
        {
            public STSCredentials(string stsToken)
            {
                STSToken = stsToken;
            }

            public string STSToken { get; }

            public NetworkCredential GetCredential(Uri uri, string authType)
            {
                throw new NotSupportedException();
            }
#endif
        }
    }
}
