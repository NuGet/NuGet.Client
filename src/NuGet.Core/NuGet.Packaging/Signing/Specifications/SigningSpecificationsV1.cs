// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Common;
using System.Linq;

namespace NuGet.Packaging.Signing
{
    public sealed class SigningSpecificationsV1 : SigningSpecifications
    {
        private const string _signaturePath = ".signature";
        private const string _SHA256String = "SHA256";
        private const string _SHA384String = "SHA384";
        private const string _SHA512String = "SHA512";
        private const int _majorVersion = 0;
        private const int _minorVersion = 9;
        private const string _authorCodeSigningExtendedKeyUsageOID = "1.3.6.1.5.5.7.3.3";
        private const string _repositoryCodeSigningExtendedKeyUsageOID = "1.3.6.1.4.1.311.84.2.1";
        private const int _rsaPublicKeyMinLength = 2048;
        private const int _ecdsaPublicKeyMinPrimeCurve = 256;
        private static readonly byte[] _ecdsaSecP256r1 = new byte[] { 0x06, 0x08, 0x2A, 0x86, 0x48, 0xCE, 0x3D, 0x03, 0x01, 0x07 };
        private static readonly byte[] _ecdsaSecP384r1 = new byte[] { 0x06, 0x05, 0x2B, 0x81, 0x04, 0x00, 0x22 };
        private static readonly byte[] _ecdsaSecP521r1 = new byte[] { 0x06, 0x05, 0x2B, 0x81, 0x04, 0x00, 0x23 };
        private static readonly byte[] _nuGetPackageSignatureFormatSignature = new byte[] { 0x81, 0x14, 0xAA, 0x4F, 0x08, 0x5B, 0x43, 0xAF, 0xAC, 0xD0, 0x49, 0xD1, 0xE1, 0x6E, 0x2F, 0x2C };
        
        /// <summary>
        /// Allowed digest algorithms for signature and timestamp hashing.
        /// </summary>
        private static readonly string[] _allowedHashAlgorithms = new[]
        {
            _SHA256String,
            _SHA384String,
            _SHA512String
        };

        /// <summary>
        /// Allowed digest algorithm Oids for signature and timestamp hashing.
        /// </summary>
        private static readonly string[] _allowedHashAlgorithmOids = new[]
        {
            HashAlgorithmName.SHA256.ConvertToOidString(),
            HashAlgorithmName.SHA384.ConvertToOidString(),
            HashAlgorithmName.SHA512.ConvertToOidString()
        };

        private static readonly byte[][] _validCurves = new[]
        {
            _ecdsaSecP256r1,
            _ecdsaSecP384r1,
            _ecdsaSecP521r1
        };

        private static readonly string[] _supportedVersions = new[]
        {
            "0.9"
        };

        public override string SignaturePath => _signaturePath;

        public override string[] AllowedHashAlgorithms => _allowedHashAlgorithms;

        public override string[] AllowedHashAlgorithmOids => _allowedHashAlgorithmOids;

        public string AuthorKeyUsageOID => _authorCodeSigningExtendedKeyUsageOID;

        public string RepositoryKeyUsageOID => _repositoryCodeSigningExtendedKeyUsageOID;

        public int RSAPublicKeyMinLength => _rsaPublicKeyMinLength;

        public int ECDSAPublicKeyMinPrimeCurve => _ecdsaPublicKeyMinPrimeCurve;

        public bool IsECDsaPublicKeyCurveValid(byte[] ECDsaCurve)
        {
            foreach (var curve in _validCurves)
            {
                if (CurvesAreEqual(curve, ECDsaCurve))
                {
                    return true;
                }
            }
            return false;
        }

        public string[] SupportedVersions = _supportedVersions;

        public byte[] NuGetPackageSignatureFormatSignature => _nuGetPackageSignatureFormatSignature;

        private bool CurvesAreEqual(byte[] curve1, byte[] curve2)
        {
            if (curve1.Length != curve2.Length)
            {
                return false;
            }
            for (var i = 0; i < curve1.Length; i++)
            {
                if (curve1[i] != curve2[i])
                {
                    return false;
                }
            }
            return true;
        }

        public SigningSpecificationsV1()
            : base()
        {
        }
    }
}
