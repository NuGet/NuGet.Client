// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;

namespace NuGet.Common
{
    public static class CryptoHashUtility
    {

        /// <summary>
        /// Compute the hash as a base64 encoded string.
        /// </summary>
        /// <remarks>Closes the stream by default.</remarks>
        /// <param name="hashAlgorithm">Algorithm to use for hashing.</param>
        /// <param name="data">Stream to hash.</param>
        public static string ComputeHashAsBase64(this HashAlgorithm hashAlgorithm, Stream data)
        {
            return ComputeHashAsBase64(hashAlgorithm, data, leaveStreamOpen: false);
        }

        /// <summary>
        /// Compute the hash as a base64 encoded string.
        /// </summary>
        /// <param name="hashAlgorithm">Algorithm to use for hashing.</param>
        /// <param name="data">Stream to hash.</param>
        /// <param name="leaveStreamOpen">If false the stream will be closed.</param>
        /// <returns>A base64 encoded hash string.</returns>
        public static string ComputeHashAsBase64(this HashAlgorithm hashAlgorithm, Stream data, bool leaveStreamOpen)
        {
            if (hashAlgorithm == null)
            {
                throw new ArgumentNullException(nameof(hashAlgorithm));
            }

            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            string hash = null;

            try
            {
                hash = Convert.ToBase64String(hashAlgorithm.ComputeHash(data));
            }
            finally
            {
                if (!leaveStreamOpen)
                {
                    data.Dispose();
                }
            }

            return hash;
        }

        public static HashAlgorithm GetHashAlgorithm(string hashAlgorithmName)
        {
            if (hashAlgorithmName == null)
            {
                throw new ArgumentNullException(nameof(hashAlgorithmName));
            }

            Enum.TryParse<HashAlgorithmName>(hashAlgorithmName, ignoreCase: true, result: out var result);

            return GetHashAlgorithm(result);
        }

        public static HashAlgorithm GetHashAlgorithm(HashAlgorithmName hashAlgorithmName)
        {
            return hashAlgorithmName.GetHashProvider();
        }

        public static HashAlgorithm GetHashProvider(this HashAlgorithmName hashAlgorithmName)
        {
#if !IS_CORECLR
            if (AllowFipsAlgorithmsOnly.Value)
            {
                // FIPs
                switch (hashAlgorithmName)
                {
                    case HashAlgorithmName.SHA256:
                        return new SHA256CryptoServiceProvider();
                    case HashAlgorithmName.SHA384:
                        return new SHA384CryptoServiceProvider();
                    case HashAlgorithmName.SHA512:
                        return new SHA512CryptoServiceProvider();
                }
            }
            else
            {
                // Non-FIPS
                switch (hashAlgorithmName)
                {
                    case HashAlgorithmName.SHA256:
                        return new SHA256Managed();
                    case HashAlgorithmName.SHA384:
                        return new SHA384Managed();
                    case HashAlgorithmName.SHA512:
                        return new SHA512Managed();
                }
            }
#else
            switch(hashAlgorithmName)
            {
                case HashAlgorithmName.SHA256:
                    return SHA256.Create();
                case HashAlgorithmName.SHA384:
                    return SHA384.Create();
                case HashAlgorithmName.SHA512:
                    return SHA512.Create();
            }
#endif

            throw new ArgumentException(nameof(hashAlgorithmName));
        }

        // Read this value once.
        private static Lazy<bool> AllowFipsAlgorithmsOnly = new Lazy<bool>(() => ReadFipsConfigValue());

        /// <summary>
        /// Determines if we are to only allow Fips compliant algorithms.
        /// </summary>
        /// <remarks>
        /// CryptoConfig.AllowOnlyFipsAlgorithm does not exist in Mono.
        /// </remarks>
        private static bool ReadFipsConfigValue()
        {
#if !IS_CORECLR
            // Mono does not currently support this method. Have this in a separate method to avoid JITing exceptions.
            var cryptoConfig = typeof(CryptoConfig);

            if (cryptoConfig != null)
            {
                var allowOnlyFipsAlgorithmsProperty = cryptoConfig.GetProperty("AllowOnlyFipsAlgorithms", BindingFlags.Public | BindingFlags.Static);

                if (allowOnlyFipsAlgorithmsProperty != null)
                {
                    return (bool)allowOnlyFipsAlgorithmsProperty.GetValue(null, null);
                }
            }

            return false;
#else
            return false;
#endif
        }
    }
}
