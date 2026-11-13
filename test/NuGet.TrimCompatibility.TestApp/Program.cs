// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

// Mirrors the repository signatures document that nuget.org serves from
// https://api.nuget.org/v3-index/repository-signatures/5.0.0/index.json. Every source repository
// whose service index advertises a RepositorySignatures resource (nuget.org does) causes this
// document to be fetched and deserialized before package version or download resources are served.
const string RepositorySignaturesJson = """
    {
      "allRepositorySigned": true,
      "signingCertificates": [
        {
          "fingerprints": {
            "2.16.840.1.101.3.4.2.1": "0e5f38f57dc1bcc806d8494f4f90fbcedd988b46760709cbeec6f4219aa6157d"
          },
          "subject": "CN=NuGet.org Repository by Microsoft, O=NuGet.org Repository by Microsoft, L=Redmond, S=Washington, C=US",
          "issuer": "CN=DigiCert SHA2 Assured ID Code Signing CA, OU=www.digicert.com, O=DigiCert Inc, C=US",
          "notBefore": "2018-04-10T00:00:00.0000000Z",
          "notAfter": "2021-04-14T12:00:00.0000000Z",
          "contentUrl": "https://api.nuget.org/v3-index/repository-signatures/certificates/0e5f38f57dc1bcc806d8494f4f90fbcedd988b46760709cbeec6f4219aa6157d.crt"
        }
      ]
    }
    """;

SourceRepository source = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");

try
{
    // A trimmed app that does not enable the NuGet.UseSystemTextJsonDeserialization feature switch
    // (the default) deserializes repository signatures through the Newtonsoft.Json reflection-based
    // code path, which is what this app intentionally exercises.
#pragma warning disable IL2026, IL3050 // Intentionally exercising the reflection-based code path under trimming.
    var resource = new RepositorySignatureResource(JObject.Parse(RepositorySignaturesJson), source);
#pragma warning restore IL2026, IL3050

    IRepositoryCertificateInfo certificate = resource.RepositoryCertificateInfos.Single();

    if (!resource.AllRepositorySigned
        || certificate.Subject != "CN=NuGet.org Repository by Microsoft, O=NuGet.org Repository by Microsoft, L=Redmond, S=Washington, C=US"
        || certificate.Fingerprints["2.16.840.1.101.3.4.2.1"] != "0e5f38f57dc1bcc806d8494f4f90fbcedd988b46760709cbeec6f4219aa6157d"
        || certificate.NotBefore != DateTimeOffset.Parse("2018-04-10T00:00:00Z")
        || certificate.NotAfter != DateTimeOffset.Parse("2021-04-14T12:00:00Z")
        || certificate.ContentUrl != "https://api.nuget.org/v3-index/repository-signatures/certificates/0e5f38f57dc1bcc806d8494f4f90fbcedd988b46760709cbeec6f4219aa6157d.crt")
    {
        Console.Error.WriteLine("Repository signature information deserialized with unexpected values.");
        return 1;
    }
}
catch (Exception e)
{
    Console.Error.WriteLine(e);
    return 1;
}

Console.WriteLine("Passed.");
return 0;
