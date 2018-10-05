// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography;

namespace NuGet.Packaging
{
    /// <summary>
    /// A SHA-512 hash function that supports incremental hashing.
    /// 
    /// This is non-private only to facilitate unit testing.
    /// </summary>
    public sealed class Sha512HashFunction : IHashFunction
    {
        private string _hash;

#if IS_DESKTOP
        private readonly SHA512 _hashFunc;

        public Sha512HashFunction()
        {
            _hashFunc = SHA512.Create();
        }

        public void Update(byte[] data, int offset, int count)
        {
            if (_hash != null)
            {
                throw new InvalidOperationException();
            }

            _hashFunc.TransformBlock(data, offset, count, outputBuffer: null, outputOffset: 0);
        }

        public string GetHash()
        {
            if (_hash == null)
            {
                _hashFunc.TransformFinalBlock(new byte[0], inputOffset: 0, inputCount: 0);

                _hash = Convert.ToBase64String(_hashFunc.Hash);
            }

            return _hash;
        }

#elif IS_CORECLR
        private readonly IncrementalHash _hashFunc;

        public Sha512HashFunction()
        {
            _hashFunc = IncrementalHash.CreateHash(HashAlgorithmName.SHA512);
        }

        public void Update(byte[] data, int offset, int count)
        {
            if (_hash != null)
            {
                throw new InvalidOperationException();
            }

            _hashFunc.AppendData(data, offset, count);
        }

        public string GetHash()
        {
            if (_hash == null)
            {
                var hash = _hashFunc.GetHashAndReset();

                _hash = Convert.ToBase64String(hash);
            }

            return _hash;
        }
#endif

        public void Dispose()
        {
            _hashFunc.Dispose();
        }
    }
}