// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

#if NET5_0_OR_GREATER

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Security.Cryptography.X509Certificates;

namespace NuGet.Packaging.Signing
{
    internal sealed class FallbackCertificateBundleX509ChainFactory : CertificateBundleX509ChainFactory
    {
        // These constants are dictated by the .NET SDK.
        internal const string SubdirectoryName = "trustedroots";
        internal const string CodeSigningFileName = "codesignctl.pem";
        internal const string TimestampingFileName = "timestampctl.pem";

        private FallbackCertificateBundleX509ChainFactory(X509Certificate2Collection certificates, string filePath)
            : base(certificates, filePath)
        {
        }

        internal static bool TryCreate(
            X509StorePurpose storePurpose,
            string fileName,
            out FallbackCertificateBundleX509ChainFactory factory)
        {
            factory = null;

            if (!TryGetThisAssemblyDirectoryPath(out string assemblyDirectoryPath))
            {
                return false;
            }

            if (string.IsNullOrEmpty(fileName))
            {
                fileName = storePurpose switch
                {
                    X509StorePurpose.CodeSigning => CodeSigningFileName,
                    X509StorePurpose.Timestamping => TimestampingFileName,
                    _ => throw new ArgumentException(Strings.InvalidX509StorePurpose, nameof(storePurpose))
                };
            }

            string fullFilePath = Path.Combine(
                assemblyDirectoryPath,
                SubdirectoryName,
                fileName);

            if (TryImportFromPemFile(fullFilePath, out X509Certificate2Collection certificates))
            {
                factory = new FallbackCertificateBundleX509ChainFactory(certificates, fullFilePath);

                return true;
            }

            return false;
        }

        [UnconditionalSuppressMessage(
            "SingleFile",
            "IL3000",
            Justification = "Assembly.Location may be empty in single file scenarios. When empty, TryCreate will return false.")]
        private static bool TryGetThisAssemblyDirectoryPath(out string directoryPath)
        {
            string location = typeof(FallbackCertificateBundleX509ChainFactory).Assembly.Location;

            if (string.IsNullOrEmpty(location))
            {
                directoryPath = null;
                return false;
            }

            directoryPath = Path.GetDirectoryName(location);
            return true;
        }
    }
}

#endif
