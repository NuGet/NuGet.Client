// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging.Signing;

namespace NuGet.CommandLine
{
    public static class CommandLineUtility
    {
        public static void ValidateSource(string source)
        {
            Uri result;
            if (!Uri.TryCreate(source, UriKind.Absolute, out result))
            {
                throw new CommandLineException(LocalizedResourceManager.GetString("InvalidSource"), source);
            }
        }

        public static string GetSourceDisplayName(string source)
        {
            if (String.IsNullOrEmpty(source) || source.Equals(NuGetConstants.DefaultGalleryServerUrl, StringComparison.OrdinalIgnoreCase))
            {
                return LocalizedResourceManager.GetString("LiveFeed") + " (" + NuGetConstants.DefaultGalleryServerUrl + ")";
            }
            if (source.Equals(NuGetConstants.DefaultSymbolServerUrl, StringComparison.OrdinalIgnoreCase))
            {
                return LocalizedResourceManager.GetString("DefaultSymbolServer") + " (" + NuGetConstants.DefaultSymbolServerUrl + ")";
            }
            return "'" + source + "'";
        }

        public static bool IsValidConfigFileName(string fileName)
        {
            return fileName != null &&
                fileName.StartsWith("packages.", StringComparison.OrdinalIgnoreCase) &&
                fileName.EndsWith(".config", StringComparison.OrdinalIgnoreCase);
        }

        public static HashAlgorithmName ValidateAndParseHashAlgorithm(string value, string name, SigningSpecifications spec)
        {
            var hashAlgorithm = HashAlgorithmName.SHA256;

            if (!string.IsNullOrEmpty(value))
            {
                if (!spec.AllowedHashAlgorithms.Contains(value, StringComparer.OrdinalIgnoreCase))
                {
                    throw new ArgumentException(string.Format(CultureInfo.CurrentCulture,
                        NuGetCommand.CommandInvalidArgumentException,
                        name));
                }

                hashAlgorithm = CryptoHashUtility.GetHashAlgorithmName(value);
            }

            if (hashAlgorithm == HashAlgorithmName.Unknown)
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture,
                        NuGetCommand.CommandInvalidArgumentException,
                        name));
            }

            return hashAlgorithm;
        }
    }
}