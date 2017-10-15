// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    public class TestSignatureProvider : ISignatureProvider
    {
        private readonly Signature _signature;

        public TestSignatureProvider(Signature signature)
        {
            _signature = signature ?? throw new ArgumentNullException(nameof(signature));
        }

        public Task<Signature> CreateSignatureAsync(SignPackageRequest request, string manifestHash, ILogger logger, CancellationToken token)
        {
            var signedJson = new JObject();
            var signatures = new JArray();
            signedJson.Add(new JProperty("signatures", signatures));
            var sig = new JObject();
            signatures.Add(sig);
            sig.Add(new JProperty("trust", _signature.TestTrust.ToString()));
            sig.Add(new JProperty("type", _signature.Type.ToString()));
            sig.Add(new JProperty("name", _signature.DisplayName));

            var result = new Signature()
            {
                DisplayName = _signature.DisplayName,
                Type = _signature.Type,
                TestTrust = _signature.TestTrust,
                Data = _signature.Data
            };

            if (result.Data == null)
            {
                result.Data = Encoding.UTF8.GetBytes(sig.ToString());
            }

            return Task.FromResult(result);
        }
    }
}
