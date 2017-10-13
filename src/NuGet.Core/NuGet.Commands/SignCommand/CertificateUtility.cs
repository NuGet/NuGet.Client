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

            certStringBuilder.AppendLine($"Issued to: {cert.IssuerName}");
            certStringBuilder.AppendLine($"Issued by: {cert.IssuerName}");
            certStringBuilder.AppendLine($"Expires: {cert.IssuerName}");
            certStringBuilder.AppendLine($"SHA1 hash: {cert.Thumbprint}");
            certStringBuilder.AppendLine($"Subject Name: {cert.SubjectName}");

            return certStringBuilder.ToString();
        }
    }
}
