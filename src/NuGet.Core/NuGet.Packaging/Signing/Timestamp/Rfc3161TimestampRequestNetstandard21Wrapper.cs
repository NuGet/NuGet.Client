using System;
using System.Collections.Generic;
using System.Linq;
#if IS_SIGNING_SUPPORTED && NETSTANDARD2_1
using System.Net.Http;
using System.Net.Http.Headers;
#endif
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Packaging.Signing
{
#if IS_SIGNING_SUPPORTED && NETSTANDARD2_1
    public class Rfc3161TimestampRequestNetstandard21Wrapper : IRfc3161TimestampRequest
    {
        private System.Security.Cryptography.Pkcs.Rfc3161TimestampRequest _rfc3161TimestampRequest;
        public Rfc3161TimestampRequestNetstandard21Wrapper(
                byte[] messageHash,
                HashAlgorithmName hashAlgorithm,
                Oid requestedPolicyId,
                byte[] nonce,
                bool requestSignerCertificates,
                X509ExtensionCollection extensions)
        {
            _rfc3161TimestampRequest = System.Security.Cryptography.Pkcs.Rfc3161TimestampRequest.CreateFromHash(
                new ReadOnlyMemory<byte>(messageHash),
                hashAlgorithm,
                requestedPolicyId,
                nonce,
                requestSignerCertificates,
                extensions);
        }

        public async Task<IRfc3161TimestampToken> SubmitRequestAsync(Uri timestampUri, TimeSpan timeout)
        {
            if (timestampUri == null)
                throw new ArgumentNullException(nameof(timestampUri));
            if (!timestampUri.IsAbsoluteUri)
                throw new ArgumentException("Absolute URI required", nameof(timestampUri));
            if (timestampUri.Scheme != Uri.UriSchemeHttp && timestampUri.Scheme != Uri.UriSchemeHttps)
                throw new ArgumentException("HTTP/HTTPS required", nameof(timestampUri));

            var client = new HttpClient();
            var content = new ReadOnlyMemoryContent(_rfc3161TimestampRequest.Encode());

            content.Headers.ContentType = new MediaTypeHeaderValue("application/timestamp-query");
            var httpResponse = await client.PostAsync(timestampUri, content);

            if (!httpResponse.IsSuccessStatusCode)
            {
                throw new CryptographicException(
                    $"There was a error from the timestamp authority. It responded with {httpResponse.StatusCode} {(int)httpResponse.StatusCode}: {httpResponse.Content}");
            }

            if (httpResponse.Content.Headers.ContentType.MediaType != "application/timestamp-response")
            {
                throw new CryptographicException("The reply from the time stamp server was in a invalid format.");
            }

            var data = await httpResponse.Content.ReadAsByteArrayAsync();   

            var response = _rfc3161TimestampRequest.ProcessResponse(data, out int bytesConsumed);

            IRfc3161TimestampToken timestampToken = new Rfc3161TimestampTokenNetstandard21Wrapper(response);

            return timestampToken;
        }
    }
#endif
}
