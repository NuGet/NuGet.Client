// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Linq;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging.Signing;

namespace NuGet.CommandLine
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public static class CommandLineUtility
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public static void ValidateSource(string source)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            Uri result;
            if (!Uri.TryCreate(source, UriKind.Absolute, out result))
            {
                throw new CommandException(LocalizedResourceManager.GetString("InvalidSource"), source);
            }
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public static string GetSourceDisplayName(string source)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            if (string.IsNullOrEmpty(source) || source.Equals(NuGetConstants.DefaultGalleryServerUrl, StringComparison.OrdinalIgnoreCase))
            {
                return LocalizedResourceManager.GetString("LiveFeed") + " (" + NuGetConstants.DefaultGalleryServerUrl + ")";
            }

            return "'" + source + "'";
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public static bool IsValidConfigFileName(string fileName)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            return fileName != null &&
                fileName.StartsWith("packages.", StringComparison.OrdinalIgnoreCase) &&
                fileName.EndsWith(".config", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Parses a command line argument's value to a supported hash algorithm and validates it is supported in the given specification
        /// </summary>
        /// <param name="argumentValue">Value entered by the user in the given command line argument</param>
        /// <param name="argumentName">Name of the command line argument</param>
        /// <param name="spec">Signing specification to validate parsed hash algorithm</param>
        /// <returns>Supported hash algorithm</returns>
        public static HashAlgorithmName ParseAndValidateHashAlgorithmFromArgument(string argumentValue, string argumentName, SigningSpecifications spec)
        {
            var hashAlgorithm = HashAlgorithmName.SHA256;

            if (!string.IsNullOrEmpty(argumentValue))
            {
                hashAlgorithm = CryptoHashUtility.GetHashAlgorithmName(argumentValue);
            }

            if (hashAlgorithm == HashAlgorithmName.Unknown || !spec.AllowedHashAlgorithms.Contains(hashAlgorithm))
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture,
                        NuGetCommand.CommandInvalidArgumentException,
                        argumentName));
            }

            return hashAlgorithm;
        }
    }
}
