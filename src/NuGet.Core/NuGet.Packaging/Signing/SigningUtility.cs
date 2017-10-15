// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// Utility methods for signing.
    /// </summary>
    public static class SigningUtility
    {
        public static IReadOnlyList<Signature> GetTestSignatures(Stream stream)
        {
            var signatures = new List<Signature>();

            using (var reader = new StreamReader(stream))
            using (var jsonReader = new JsonTextReader(reader))
            {
                var json = JObject.Load(jsonReader);

                foreach (var sigEntry in json.Value<JArray>("signatures"))
                {
                    Enum.TryParse<SignatureVerificationStatus>(sigEntry["trust"].Value<string>(), ignoreCase: true, result: out var testTrust);
                    Enum.TryParse<SignatureType>(sigEntry["type"].Value<string>(), ignoreCase: true, result: out var sigType);

                    signatures.Add(new Signature()
                    {
                        DisplayName = sigEntry.Value<string>("name"),
                        TestTrust = testTrust,
                        Type = sigType,
                    });
                }
            }

            return signatures.AsReadOnly();
        }

        public static bool IsCertificateValid(X509Certificate2 certificate, out X509Chain chain, bool allowUntrustedRoot)
        {
            if (certificate == null)
            {
                throw new ArgumentNullException(nameof(certificate));
            }

            chain = new X509Chain();

            if (allowUntrustedRoot)
            {
                chain.ChainPolicy.VerificationFlags |= X509VerificationFlags.AllowUnknownCertificateAuthority;
            }

            return chain.Build(certificate);
        }
    }
}
