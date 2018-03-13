// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol
{
    public class RepositorySignatureResource : INuGetResource
    {

        public bool AllRepositorySigned { get; }

        public IEnumerable<IRepositoryCertificateInfo> RepositoryCertificateInfos { get; }

        public RepositorySignatureResource(JObject repoSignInformationContent, SourceRepository source)
        {
            var allRepositorySigned = repoSignInformationContent.GetBoolean(JsonProperties.AllRepositorySigned) ??
                throw new FatalProtocolException(string.Format(CultureInfo.CurrentCulture, Strings.Log_FailedToParseRepoSignInfor, JsonProperties.AllRepositorySigned, source.PackageSource.Source));
            var data = repoSignInformationContent[JsonProperties.SigningCertificates] as JArray ??
                throw new FatalProtocolException(string.Format(CultureInfo.CurrentCulture, Strings.Log_FailedToParseRepoSignInfor, JsonProperties.SigningCertificates, source.PackageSource.Source));

            AllRepositorySigned = allRepositorySigned;
            RepositoryCertificateInfos = data.OfType<JObject>().Select(p => p.FromJToken<RepositoryCertificateInfo>());
        }

        // Test only.
        public RepositorySignatureResource(bool allRepositorySigned, IEnumerable<IRepositoryCertificateInfo> RepositoryCertificateInfos)
        {
            AllRepositorySigned = allRepositorySigned;
            RepositoryCertificateInfos = RepositoryCertificateInfos;
        }

    }
}
