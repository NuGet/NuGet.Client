// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace NuGet.Commands
{
    public static class CertificateUtility
    {
        public static string X509Certificate2ToString(X509Certificate2 cert)
        {
            var certStringBuilder = new StringBuilder();

            certStringBuilder.AppendLine($"Subject Name: {cert.SubjectName.Name}");
            certStringBuilder.AppendLine($"SHA1 hash: {cert.Thumbprint}");
            certStringBuilder.AppendLine($"Issued by: {cert.Issuer}");
            certStringBuilder.AppendLine($"Expires: {cert.NotAfter}");

            return certStringBuilder.ToString();
        }
    }
}
