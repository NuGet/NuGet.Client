// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;

namespace NuGet.Configuration
{
    /// <summary>
    /// CryptoHashProvider helps calculate or verify hash based on SHA256 or SHA512 algorithms
    /// </summary>
    public class CryptoHashProvider
    {
        /// <summary>
        /// Server token used to represent that the hash being used is SHA 512
        /// </summary>
        private const string SHA512HashAlgorithm = "SHA512";

        /// <summary>
        /// Server token used to represent that the hash being used is SHA 256
        /// </summary>
        private const string SHA256HashAlgorithm = "SHA256";

        private readonly string _hashAlgorithm;

        /// <summary>
        /// Creates an instance of CryptoHashProvider. Since the algorithm is not specified, SHA512 is assumed
        /// </summary>
        public CryptoHashProvider()
            : this(null)
        {
        }

        /// <summary>
        /// Creates an instance of CryptoHashProvider using the hashAlgorithm
        /// </summary>
        public CryptoHashProvider(string hashAlgorithm)
        {
            if (String.IsNullOrEmpty(hashAlgorithm))
            {
                hashAlgorithm = SHA512HashAlgorithm;
            }
            else if (!hashAlgorithm.Equals(SHA512HashAlgorithm, StringComparison.OrdinalIgnoreCase)
                     &&
                     !hashAlgorithm.Equals(SHA256HashAlgorithm, StringComparison.OrdinalIgnoreCase))
            {
                // Only support a vetted list of hash algorithms.
                throw new ArgumentException(String.Format(CultureInfo.CurrentCulture, Resources.UnsupportedHashAlgorithm, hashAlgorithm), "hashAlgorithm");
            }

            _hashAlgorithm = hashAlgorithm;
        }

        /// <summary>
        /// Determines if we are to only allow Fips compliant algorithms.
        /// </summary>
        /// <remarks>
        /// CryptoConfig.AllowOnlyFipsAlgorithm does not exist in Mono.
        /// </remarks>
        private static bool AllowOnlyFipsAlgorithms
        {
            get { return ReadFipsConfigValue(); }
        }

        /// <summary>
        /// Calculates the hash for a given stream
        /// </summary>
        public byte[] CalculateHash(Stream stream)
        {
            using (var hashAlgorithm = GetHashAlgorithm())
            {
                return hashAlgorithm.ComputeHash(stream);
            }
        }

        /// <summary>
        /// Calculates the hash for a byte array
        /// </summary>
        public byte[] CalculateHash(byte[] data)
        {
            using (var hashAlgorithm = GetHashAlgorithm())
            {
                return hashAlgorithm.ComputeHash(data);
            }
        }

        /// <summary>
        /// Verifies the hash for the given data and hash
        /// </summary>
        public bool VerifyHash(byte[] data, byte[] hash)
        {
            var dataHash = CalculateHash(data);
            return Enumerable.SequenceEqual(dataHash, hash);
        }

        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "We want to return the object.")]
        private HashAlgorithm GetHashAlgorithm()
        {
#if !DNXCORE50
            if (_hashAlgorithm.Equals(SHA256HashAlgorithm, StringComparison.OrdinalIgnoreCase))
            {
                return AllowOnlyFipsAlgorithms ? (HashAlgorithm)new SHA256CryptoServiceProvider() : (HashAlgorithm)new SHA256Managed();
            }

            return AllowOnlyFipsAlgorithms ? (HashAlgorithm)new SHA512CryptoServiceProvider() : (HashAlgorithm)new SHA512Managed();
#else
    // TODO: Review FIPS compliance for CoreCLR
            if (_hashAlgorithm.Equals(SHA256HashAlgorithm, StringComparison.OrdinalIgnoreCase))
            {
                return SHA256.Create();
            }

            return SHA512.Create();
#endif
        }

        private static bool ReadFipsConfigValue()
        {
#if !DNXCORE50
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
