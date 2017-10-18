// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// Remove or add signature package metadata.
    /// </summary>
    public sealed class Signer
    {
        // Temporary
        private const string TestSignedPath = "testsigned/signed.json";
        private readonly ISignedPackage _package;

        /// <summary>
        /// Creates a signer for a specific package.
        /// </summary>
        /// <param name="package">Package to sign or modify.</param>
        public Signer(ISignedPackage package)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
        }

        /// <summary>
        /// Add a signature to a package.
        /// </summary>
        public async Task SignAsync(SignPackageRequest request, ILogger logger, CancellationToken token)
        {
            var signedJson = new JObject();
            var signatures = new JArray();
            signedJson.Add(new JProperty("signatures", signatures));
            var sig = new JObject();
            signatures.Add(sig);
            sig.Add(new JProperty("trust", request.Signature.TestTrust.ToString()));
            sig.Add(new JProperty("type", request.Signature.Type.ToString()));
            sig.Add(new JProperty("name", request.Signature.DisplayName));

            using (var stream = new MemoryStream())
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(signedJson.ToString());
                writer.Flush();
                stream.Position = 0;
                await _package.AddAsync(TestSignedPath, stream, token);
            }
        }

        /// <summary>
        /// Remove all signatures from a package.
        /// </summary>
        public Task RemoveSignaturesAsync(ILogger logger, CancellationToken token)
        {
            return _package.RemoveAsync(TestSignedPath, token);
        }

        /// <summary>
        /// Remove a single signature from a package.
        /// </summary>
        public Task RemoveSignatureAsync(Signature signature, ILogger logger, CancellationToken token)
        {
            // TODO: counter signing support/removal of counter sign only
            return RemoveSignaturesAsync(logger, token);
        }
    }
}
