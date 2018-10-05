// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using NuGet.Packaging;
using Xunit;

namespace NuGet.ProjectModel.Test
{
    public class HashObjectWriterTests : IDisposable
    {
        private readonly IHashFunction _hashFunc;
        private readonly HashObjectWriter _writer;

        public HashObjectWriterTests()
        {
            _hashFunc = new Sha512HashFunction();
            _writer = new HashObjectWriter(_hashFunc);
        }

        public void Dispose()
        {
            _writer.Dispose();
            _hashFunc.Dispose();
        }

        [Fact]
        public void Constructor_ThrowsForNullHashFunc()
        {
            Assert.Throws<ArgumentNullException>(() => new HashObjectWriter(hashFunc: null));
        }

        [Fact]
        public void GetHash_HasDefaultValue()
        {
            const string expectedHash = "J8dGcK23UHX60FjVzq97IMTneGyDuuijL2Jvl4KvNMmjPCBG72D9Knh403jin+yFGAa72aZ4ePOp8c2kgwdj/Q==";
            var actualHash = _writer.GetHash();

            Assert.Equal(expectedHash, actualHash);
        }

        [Fact]
        public void GetHash_ComputesOverEntireObject()
        {
            _writer.WriteObjectStart("a");
            _writer.WriteNameValue("b", 0);
            _writer.WriteNameValue("c", "d");
            _writer.WriteNameArray("e", new[] { "f", "g" });
            _writer.WriteObjectEnd();

            const string expectedHash = "TGP0LarTsGYQ2bqAC8lWyRQR+JsKzsO0Y+h6w7mtTj6mBOLTy8Dr0ZypSgzwzD9xuddh2ceDT7fEXve5ohuNeQ==";
            var actualHash = _writer.GetHash();

            Assert.Equal(expectedHash, actualHash);
        }

        [Fact]
        public void WriteObjectStart_ThrowsForNullName()
        {
            Assert.Throws<ArgumentNullException>(() => _writer.WriteObjectStart(name: null));
        }

        [Fact]
        public void WriteObjectStart_ThrowsIfReadOnly()
        {
            MakeReadOnly();

            Assert.Throws<InvalidOperationException>(() => _writer.WriteObjectStart("a"));
        }

        [Fact]
        public void WriteObjectStart_SupportsEmptyName()
        {
            _writer.WriteObjectStart(name: "");
            _writer.WriteObjectEnd();

            const string expectedHash = "knKxm5x6Jpr2pv4LPmgc9Vt/eR3n4kSCkc18RMfY78x9B52j8BHbj0MjOK99Y28IcIpRppus2d/JoX/p5+jZHA==";
            var actualHash = _writer.GetHash();

            Assert.Equal(expectedHash, actualHash);
        }

        [Fact]
        public void WriteObjectEnd_ThrowsIfReadOnly()
        {
            _writer.WriteObjectStart("a");

            MakeReadOnly();

            Assert.Throws<InvalidOperationException>(() => _writer.WriteObjectEnd());
        }

        [Fact]
        public void WriteObjectEnd_ThrowsIfCalledOnRoot()
        {
            Assert.Throws<InvalidOperationException>(() => _writer.WriteObjectEnd());
        }

        [Fact]
        public void WriteNameValue_WithBoolValue_ThrowsForNullName()
        {
            Assert.Throws<ArgumentNullException>(() => _writer.WriteNameValue(name: null, value: true));
        }

        [Fact]
        public void WriteNameValue_WithBoolValue_ThrowsIfReadOnly()
        {
            MakeReadOnly();

            Assert.Throws<InvalidOperationException>(() => _writer.WriteNameValue("a", value: true));
        }

        [Fact]
        public void WriteNameValue_WithBoolValue_SupportsEmptyName()
        {
            _writer.WriteNameValue(name: "", value: true);

            const string expectedHash = "h+DBc/HiHXmYua4cJFF3KTsac/iwr0KN4TQNtXZdHgZA05PwtMldoPNkXQ/H+8bGw3OCxzDolEdgCkLF4F559A==";
            var actualHash = _writer.GetHash();

            Assert.Equal(expectedHash, actualHash);
        }

        [Fact]
        public void WriteNameValue_WithIntValue_ThrowsForNullName()
        {
            Assert.Throws<ArgumentNullException>(() => _writer.WriteNameValue(name: null, value: 0));
        }

        [Fact]
        public void WriteNameValue_WithIntValue_ThrowsIfReadOnly()
        {
            MakeReadOnly();

            Assert.Throws<InvalidOperationException>(() => _writer.WriteNameValue("a", value: 1));
        }

        [Fact]
        public void WriteNameValue_WithIntValue_SupportsEmptyName()
        {
            _writer.WriteNameValue(name: "", value: 3);

            const string expectedHash = "TnmYxd+nupymXRi9r+MPlKYv2xrgnd4owbVaJur49jN3sm2bQKXkFwBJIA+NlEArnz4QoMFJmomlXJDExHZKMQ==";
            var actualHash = _writer.GetHash();

            Assert.Equal(expectedHash, actualHash);
        }

        [Fact]
        public void WriteNameValue_WithStringValue_ThrowsForNullName()
        {
            Assert.Throws<ArgumentNullException>(() => _writer.WriteNameValue(name: null, value: "a"));
        }

        [Fact]
        public void WriteNameValue_WithStringValue_ThrowsIfReadOnly()
        {
            MakeReadOnly();

            Assert.Throws<InvalidOperationException>(() => _writer.WriteNameValue("a", "b"));
        }

        [Fact]
        public void WriteNameValue_WithStringValue_SupportsEmptyNameAndEmptyValue()
        {
            _writer.WriteNameValue(name: "", value: "");

            const string expectedHash = "rRT9i81nhJdNPYfhfGo8b7u0lFCjmweC7fi4Gs3bE3aNzMrMLetck4BSgHTJ8DxbzomkNRsYNFKdROBep1HIXw==";
            var actualHash = _writer.GetHash();

            Assert.Equal(expectedHash, actualHash);
        }

        [Fact]
        public void WriteNameArray_ThrowsForNullName()
        {
            Assert.Throws<ArgumentNullException>(() => _writer.WriteNameArray(name: null, values: new[] { "a" }));
        }

        [Fact]
        public void WriteNameArray_ThrowsIfReadOnly()
        {
            MakeReadOnly();

            Assert.Throws<InvalidOperationException>(() => _writer.WriteNameArray("a", new[] { "b" }));
        }

        [Fact]
        public void WriteNameArray_SupportsEmptyNameAndEmptyValues()
        {
            _writer.WriteNameArray(name: "", values: Enumerable.Empty<string>());

            const string expectedHash = "KPMSRUKjEBMaUdDcHOxPO/bVXtH5ITcjM9/Fq/BhWy4v9ZJxkOA0rMe7+6Uxc+s6bLv+zTcr4O+UfJ7ksu5k1Q==";
            var actualHash = _writer.GetHash();

            Assert.Equal(expectedHash, actualHash);
        }

        private void MakeReadOnly()
        {
            _writer.GetHash();
        }
    }
}