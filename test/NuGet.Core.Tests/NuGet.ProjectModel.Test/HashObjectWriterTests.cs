// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.RuntimeModel;
using NuGet.Versioning;
using Xunit;

namespace NuGet.ProjectModel.Test
{
    public class HashObjectWriterTests : IDisposable
    {
        private const string PropertyName = "a";

        private readonly IHashFunction _hashFunc;
        private readonly HashObjectWriter _writer;

        public HashObjectWriterTests()
        {
            _hashFunc = new FnvHash64Function();
            _writer = new HashObjectWriter(_hashFunc);
        }

        public void Dispose()
        {
            _writer.Dispose();
            _hashFunc.Dispose();
        }

        [Fact]
        public void Constructor_WhenHashFuncIsNull_Throws()
        {
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
                () => new HashObjectWriter(hashFunc: null));

            Assert.Equal("hashFunc", exception.ParamName);
        }

        [Fact]
        public void WriteNameRawValue_WhenNameIsNull_Throws()
        {
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => _writer.WriteNameRawValue(null, "{}"));
            Assert.Equal("name", exception.ParamName);
        }

        [Fact]
        public void WriteNameRawValue_WhenJsonIsNull_Throws()
        {
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => _writer.WriteNameRawValue(PropertyName, null));
            Assert.Equal("json", exception.ParamName);
        }

        [Fact]
        public void WriteNameRawValue_WhenDisposed_Throws()
        {
            _writer.Dispose();
            Assert.Throws<ObjectDisposedException>(() => _writer.WriteNameRawValue(PropertyName, "{}"));
        }

        [Fact]
        public void WriteNameRawValue_WhenReadOnly_Throws()
        {
            MakeReadOnly();
            Assert.Throws<InvalidOperationException>(() => _writer.WriteNameRawValue(PropertyName, "{}"));
        }

        [Fact]
        public void SerializeObject_WhenDisposed_Throws()
        {
            _writer.Dispose();
            Assert.Throws<ObjectDisposedException>(() =>
                _writer.SerializeObject(Array.Empty<KeyValuePair<string, string>>(), static (_, _) => { }));
        }

        [Fact]
        public void SerializeObject_WhenReadOnly_Throws()
        {
            MakeReadOnly();
            Assert.Throws<InvalidOperationException>(() =>
                _writer.SerializeObject(Array.Empty<KeyValuePair<string, string>>(), static (_, _) => { }));
        }

        [Fact]
        public void SerializeObject_WithReusedBuffer_PreservesPreviousResult()
        {
            string first = _writer.SerializeObject(new[] { new KeyValuePair<string, string>("first", "value") }, WriteValues);
            string second = _writer.SerializeObject(new[] { new KeyValuePair<string, string>("second", "another value") }, WriteValues);

            Assert.Equal("{\"first\":\"value\"}", first);
            Assert.Equal("{\"second\":\"another value\"}", second);

            static void WriteValues(NuGet.RuntimeModel.IObjectWriter writer, KeyValuePair<string, string>[] values)
            {
                foreach (var value in values)
                {
                    writer.WriteNameValue(value.Key, value.Value);
                }
            }
        }

        [Fact]
        public void GetOrSerializeCentralPackageVersions_WithEquivalentWrittenContent_SerializesOnce()
        {
            var first = new[] { new KeyValuePair<string, CentralPackageVersion>("FirstKey", new CentralPackageVersion("Package", VersionRange.Parse("1.0.0"))) };
            var second = new[] { new KeyValuePair<string, CentralPackageVersion>("DifferentKey", new CentralPackageVersion("Package", VersionRange.Parse("1.0.0"))) };
            int serializations = 0;

            SerializedPackageReferences<CentralPackageVersion> original = _writer.GetOrSerializeCentralPackageVersions(first, WriteValues);
            SerializedPackageReferences<CentralPackageVersion> shared = _writer.GetOrSerializeCentralPackageVersions(second, WriteValues);

            Assert.Same(original, shared);
            Assert.Equal(1, serializations);
            Assert.Equal("{\"Package\":\"1.0.0\"}", shared.Json);

            void WriteValues(IObjectWriter output, KeyValuePair<string, CentralPackageVersion>[] values)
            {
                serializations++;
                foreach (var value in values)
                {
                    output.WriteNameValue(value.Value.Name, PackageSpecWriter.GetVersionString(value.Value.VersionRange));
                }
            }
        }

        [Fact]
        public void GetOrSerializePackagesToPrune_WithEquivalentWrittenContent_SerializesOnce()
        {
            var first = new[] { new KeyValuePair<string, PrunePackageReference>("Package", PrunePackageReference.Create("FirstName", "1.0.0")) };
            var second = new[] { new KeyValuePair<string, PrunePackageReference>("Package", PrunePackageReference.Create("DifferentName", "1.0.0")) };
            int serializations = 0;

            SerializedPackageReferences<PrunePackageReference> original = _writer.GetOrSerializePackagesToPrune(first, WriteValues);
            SerializedPackageReferences<PrunePackageReference> shared = _writer.GetOrSerializePackagesToPrune(second, WriteValues);

            Assert.Same(original, shared);
            Assert.Equal(1, serializations);

            void WriteValues(IObjectWriter output, KeyValuePair<string, PrunePackageReference>[] values)
            {
                serializations++;
                WritePruningValues(output, values);
            }
        }

        [Theory]
        [InlineData("name")]
        [InlineData("case")]
        [InlineData("version")]
        [InlineData("version-text")]
        [InlineData("order")]
        [InlineData("length")]
        [InlineData("middle")]
        public void GetOrSerializePackagesToPrune_WithDifferentWrittenContent_DoesNotShare(string change)
        {
            KeyValuePair<string, PrunePackageReference>[] originalItems = CreatePruningValues();
            KeyValuePair<string, PrunePackageReference>[] changed = CreatePruningValues();
            switch (change)
            {
                case "name":
                    changed[0] = new KeyValuePair<string, PrunePackageReference>("another", changed[0].Value);
                    break;
                case "case":
                    changed[1] = new KeyValuePair<string, PrunePackageReference>("B", changed[1].Value);
                    break;
                case "version":
                    changed[0] = new KeyValuePair<string, PrunePackageReference>("a", PrunePackageReference.Create("a", "2.0.0"));
                    break;
                case "version-text":
                    changed[0] = new KeyValuePair<string, PrunePackageReference>("a", new PrunePackageReference("a", VersionRange.Parse("(,1.0]")));
                    break;
                case "order":
                    Array.Reverse(changed);
                    break;
                case "length":
                    changed = changed.Take(2).ToArray();
                    break;
                case "middle":
                    changed[1] = new KeyValuePair<string, PrunePackageReference>("b", PrunePackageReference.Create("b", "2.0.0"));
                    break;
            }

            SerializedPackageReferences<PrunePackageReference> original = _writer.GetOrSerializePackagesToPrune(originalItems, WritePruningValues);
            SerializedPackageReferences<PrunePackageReference> different = _writer.GetOrSerializePackagesToPrune(changed, WritePruningValues);

            Assert.NotSame(original, different);
            Assert.NotEqual(original.Json, different.Json);
        }

        [Fact]
        public void SerializedPackageReferenceComparer_WithHashCollision_ComparesFullContent()
        {
            var comparer = new SerializedPackageReferenceComparer<PrunePackageReference>(static item => item.Key, static value => value.VersionRange);
            KeyValuePair<string, PrunePackageReference>[] first = CreatePruningValues();
            KeyValuePair<string, PrunePackageReference>[] second = CreatePruningValues();
            second[1] = new KeyValuePair<string, PrunePackageReference>("b", PrunePackageReference.Create("b", "2.0.0"));
            var firstKey = new SerializedPackageReferenceKey<PrunePackageReference>(first, hashCode: 7);
            var secondKey = new SerializedPackageReferenceKey<PrunePackageReference>(second, hashCode: 7);
            var cache = new Dictionary<SerializedPackageReferenceKey<PrunePackageReference>, string>(comparer)
            {
                [firstKey] = "first",
                [secondKey] = "second"
            };

            Assert.Equal(2, cache.Count);
            Assert.Equal("first", cache[firstKey]);
            Assert.Equal("second", cache[secondKey]);
        }

        [Fact]
        public void GetOrSerializePackagesToPrune_WithDifferentWriters_DoesNotUseGlobalCache()
        {
            using var other = new HashObjectWriter(new FnvHash64Function());
            SerializedPackageReferences<PrunePackageReference> first = _writer.GetOrSerializePackagesToPrune(CreatePruningValues(), WritePruningValues);
            SerializedPackageReferences<PrunePackageReference> second = other.GetOrSerializePackagesToPrune(CreatePruningValues(), WritePruningValues);

            Assert.NotSame(first, second);
            Assert.Equal(first.Json, second.Json);
        }

        [Fact]
        public void GetOrSerializeCentralPackageVersions_WithGeneratedVersionText_DoesNotCache()
        {
            var items = new[]
            {
                new KeyValuePair<string, CentralPackageVersion>("Package", new CentralPackageVersion("Package", new VersionRange(new NuGetVersion(1, 0, 0))))
            };

            Assert.Null(_writer.GetOrSerializeCentralPackageVersions(items, static (_, _) => throw new InvalidOperationException("Serialization must not be attempted.")));
        }

        [Fact]
        public void GetOrSerializePackagesToPrune_WhenReadOnly_ThrowsEvenForCachedContent()
        {
            KeyValuePair<string, PrunePackageReference>[] items = CreatePruningValues();
            _writer.GetOrSerializePackagesToPrune(items, WritePruningValues);
            MakeReadOnly();

            Assert.Throws<InvalidOperationException>(() => _writer.GetOrSerializePackagesToPrune(items, WritePruningValues));
        }

        [Fact]
        public void Dispose_WithSerializationScope_ReleasesScopeAndOperationCache()
        {
            HashObjectWriter writer = CreateWriterWithScope(out WeakReference scope, out WeakReference section);
            writer.Dispose();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Assert.False(scope.IsAlive);
            Assert.False(section.IsAlive);
            GC.KeepAlive(writer);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static HashObjectWriter CreateWriterWithScope(out WeakReference scopeReference, out WeakReference sectionReference)
        {
            var scope = new[] { new KeyValuePair<string, PackageSpec>("Project", new PackageSpec()) };
            var writer = new HashObjectWriter(new FnvHash64Function(), scope);
            SerializedPackageReferences<PrunePackageReference> section = writer.GetOrSerializePackagesToPrune(CreatePruningValues(), WritePruningValues);
            Assert.NotNull(section);
            scopeReference = new WeakReference(scope);
            sectionReference = new WeakReference(section);
            return writer;
        }

        private static KeyValuePair<string, PrunePackageReference>[] CreatePruningValues()
        {
            return new[]
            {
                new KeyValuePair<string, PrunePackageReference>("a", PrunePackageReference.Create("a", "1.0.0")),
                new KeyValuePair<string, PrunePackageReference>("b", PrunePackageReference.Create("b", "1.0.0")),
                new KeyValuePair<string, PrunePackageReference>("c", PrunePackageReference.Create("c", "1.0.0"))
            };
        }

        private static void WritePruningValues(IObjectWriter writer, KeyValuePair<string, PrunePackageReference>[] values)
        {
            foreach (var value in values)
            {
                writer.WriteNameValue(value.Key, PackageSpecWriter.GetVersionString(value.Value.VersionRange));
            }
        }

        [Fact]
        public void Dispose_Always_IsIdempotent()
        {
            _writer.Dispose();
            _writer.Dispose();
        }

        [Fact]
        public void WriteObjectStart_WithNoParameter_WhenDisposed_Throws()
        {
            _writer.Dispose();

            Assert.Throws<ObjectDisposedException>(() => _writer.WriteObjectStart());
        }

        [Fact]
        public void WriteObjectStart_WithNoParameter_WhenReadOnly_Throws()
        {
            MakeReadOnly();

            Assert.Throws<InvalidOperationException>(() => _writer.WriteObjectStart());
        }

        [Fact]
        public void WriteObjectStart_WithNoParameter_WhenCalled_WritesObjectStart()
        {
            _writer.WriteObjectStart();

            const string expectedHash = "uhgChkz2Y68=";
            string actualHash = _writer.GetHash();

            Assert.Equal(expectedHash, actualHash);
        }

        [Fact]
        public void WriteObjectStart_WithName_WhenNameIsNull_Throws()
        {
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
                () => _writer.WriteObjectStart(name: null));

            Assert.Equal("name", exception.ParamName);
        }

        [Fact]
        public void WriteObjectStart_WithName_WhenDisposed_Throws()
        {
            _writer.Dispose();

            Assert.Throws<ObjectDisposedException>(() => _writer.WriteObjectStart(PropertyName));
        }

        [Fact]
        public void WriteObjectStart_WithName_WhenReadOnly_Throws()
        {
            MakeReadOnly();

            Assert.Throws<InvalidOperationException>(() => _writer.WriteObjectStart(PropertyName));
        }

        [Theory]
        [InlineData("", "1fyYPzTw+J4=")]
        [InlineData(PropertyName, "aEkDaamOy8A=")]
        public void WriteObjectStart_WithName_WithValidName_WritesObjectStart(string name, string expectedHash)
        {
            _writer.WriteObjectStart();
            _writer.WriteObjectStart(name);

            string actualHash = _writer.GetHash();

            Assert.Equal(expectedHash, actualHash);
        }

        [Fact]
        public void WriteObjectEnd_WhenDisposed_Throws()
        {
            _writer.WriteObjectStart();
            _writer.Dispose();

            Assert.Throws<ObjectDisposedException>(() => _writer.WriteObjectEnd());
        }

        [Fact]
        public void WriteObjectEnd_WhenReadOnly_Throws()
        {
            _writer.WriteObjectStart();

            MakeReadOnly();

            Assert.Throws<InvalidOperationException>(() => _writer.WriteObjectEnd());
        }

        [Fact]
        public void WriteObjectEnd_WithoutObjectStart_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => _writer.WriteObjectEnd());
        }

        [Fact]
        public void WriteNameValue_WithIntValue_WhenNameIsNull_Throws()
        {
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
                () => _writer.WriteNameValue(name: null, value: 3));

            Assert.Equal("name", exception.ParamName);
        }

        [Fact]
        public void WriteNameValue_WithIntValue_WhenDisposed_Throws()
        {
            _writer.Dispose();

            Assert.Throws<ObjectDisposedException>(() => _writer.WriteNameValue(PropertyName, value: 3));
        }

        [Fact]
        public void WriteNameValue_WithIntValue_WhenReadOnly_Throws()
        {
            MakeReadOnly();

            Assert.Throws<InvalidOperationException>(() => _writer.WriteNameValue(PropertyName, value: 3));
        }

        [Theory]
        [InlineData("", -1, "5vr8D8iI2Lg=")]
        [InlineData(PropertyName, 1, "dtICaalIy8A=")]
        public void WriteNameValue_WithIntValue_WithValidValue_WritesNameValue(string name, int value, string expectedHash)
        {
            _writer.WriteObjectStart();
            _writer.WriteNameValue(name, value);

            string actualHash = _writer.GetHash();

            Assert.Equal(expectedHash, actualHash);
        }

        [Fact]
        public void WriteNameValue_WithBoolValue_WhenNameIsNull_Throws()
        {
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
                () => _writer.WriteNameValue(name: null, value: true));

            Assert.Equal("name", exception.ParamName);
        }

        [Fact]
        public void WriteNameValue_WithBoolValue_WhenDisposed_Throws()
        {
            _writer.Dispose();

            Assert.Throws<ObjectDisposedException>(() => _writer.WriteNameValue(PropertyName, value: true));
        }

        [Fact]
        public void WriteNameValue_WithBoolValue_WhenReadOnly_Throws()
        {
            MakeReadOnly();

            Assert.Throws<InvalidOperationException>(() => _writer.WriteNameValue(PropertyName, value: true));
        }

        [Theory]
        [InlineData("", true, "rE+NQworCkw=")]
        [InlineData(PropertyName, false, "0rT5fJwG03I=")]
        public void WriteNameValue_WithBoolValue_WithValidValue_WritesNameValue(string name, bool value, string expectedHash)
        {
            _writer.WriteObjectStart();
            _writer.WriteNameValue(name, value);

            string actualHash = _writer.GetHash();

            Assert.Equal(expectedHash, actualHash);
        }

        [Fact]
        public void WriteNameValue_WithStringValue_WhenNameIsNull_Throws()
        {
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
                () => _writer.WriteNameValue(name: null, value: "b"));

            Assert.Equal("name", exception.ParamName);
        }

        [Fact]
        public void WriteNameValue_WithStringValue_WhenDisposed_Throws()
        {
            _writer.Dispose();

            Assert.Throws<ObjectDisposedException>(() => _writer.WriteNameValue(PropertyName, value: "b"));
        }

        [Fact]
        public void WriteNameValue_WithStringValue_WhenReadOnly_Throws()
        {
            MakeReadOnly();

            Assert.Throws<InvalidOperationException>(() => _writer.WriteNameValue(PropertyName, value: "b"));
        }

        [Theory]
        [InlineData("", "", "OAojEMgFBbk=")]
        [InlineData(PropertyName, null, "ViPwxht58Ok=")]
        [InlineData(PropertyName, "b", "kwmmpEQYi7g=")]
        public void WriteNameValue_WithStringValue_WithValidValue_WritesNameValue(string name, string value, string expectedHash)
        {
            _writer.WriteObjectStart();
            _writer.WriteNameValue(name, value);

            string actualHash = _writer.GetHash();

            Assert.Equal(expectedHash, actualHash);
        }

        [Fact]
        public void WriteNameArray_WhenNameIsNull_Throws()
        {
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
                () => _writer.WriteNameArray(name: null, values: new[] { "b" }));

            Assert.Equal("name", exception.ParamName);
        }

        [Fact]
        public void WriteNameArray_WhenDisposed_Throws()
        {
            _writer.Dispose();

            Assert.Throws<ObjectDisposedException>(() => _writer.WriteNameArray(PropertyName, new[] { "b" }));
        }

        [Fact]
        public void WriteNameArray_WhenReadOnly_Throws()
        {
            MakeReadOnly();

            Assert.Throws<InvalidOperationException>(() => _writer.WriteNameArray(PropertyName, new[] { "b" }));
        }

        [Theory]
        [InlineData("", null, "uFlSEcjwabo=")]
        [InlineData(PropertyName, "b", "Td9cKXXB7Gk=")]
        public void WriteNameArray_WithValidValues_WritesNameArray(string name, string value, string expectedHash)
        {
            IEnumerable<string> values = value == null ? Enumerable.Empty<string>() : new[] { value };

            _writer.WriteObjectStart();
            _writer.WriteNameArray(name, values);

            string actualHash = _writer.GetHash();

            Assert.Equal(expectedHash, actualHash);
        }

        [Fact]
        public void WriteNameArray_WithNullValue_WritesNameArray()
        {
            _writer.WriteObjectStart();
            _writer.WriteNameArray(PropertyName, new string[] { null });

            const string expectedHash = "6JqdqL+bz8M=";
            string actualHash = _writer.GetHash();

            Assert.Equal(expectedHash, actualHash);
        }

        [Fact]
        public void WriteNonEmptyNameArray_WhenNameIsNull_Throws()
        {
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
                () => _writer.WriteNonEmptyNameArray(name: null, values: new[] { "b" }));

            Assert.Equal("name", exception.ParamName);
        }

        [Fact]
        public void WriteNonEmptyNameArray_WhenDisposed_Throws()
        {
            _writer.Dispose();

            Assert.Throws<ObjectDisposedException>(() => _writer.WriteNonEmptyNameArray(PropertyName, new[] { "b" }));
        }

        [Fact]
        public void WriteNonEmptyNameArray_WhenReadOnly_Throws()
        {
            MakeReadOnly();

            Assert.Throws<InvalidOperationException>(() => _writer.WriteNonEmptyNameArray(PropertyName, new[] { "b" }));
        }

        [Theory]
        [InlineData(PropertyName, "b", "Td9cKXXB7Gk=")]
        public void WriteNonEmptyNameArray_WithValidValues_WritesNameArray(string name, string value, string expectedHash)
        {
            IEnumerable<string> values = value == null ? Enumerable.Empty<string>() : new[] { value };

            _writer.WriteObjectStart();
            _writer.WriteNonEmptyNameArray(name, values);

            string actualHash = _writer.GetHash();

            Assert.Equal(expectedHash, actualHash);
        }

        [Theory]
        [InlineData(PropertyName, "b", "Td9cKXXB7Gk=")]
        public void WriteNonEmptyNameArray_WithEmptyValues_DoesNotWriteNameArray(string name, string value, string expectedHash)
        {
            IEnumerable<string> values = value == null ? Enumerable.Empty<string>() : new[] { value };

            _writer.WriteObjectStart();
            _writer.WriteNonEmptyNameArray("FirstEmptyWrite", Enumerable.Empty<string>());
            _writer.WriteNonEmptyNameArray(name, values);
            _writer.WriteNonEmptyNameArray("SecondEmptyWrite", Enumerable.Empty<string>());

            string actualHash = _writer.GetHash();

            Assert.Equal(expectedHash, actualHash);
        }

        [Fact]
        public void WriteNonEmptyNameArray_WithNullValue_WritesNameArray()
        {
            _writer.WriteObjectStart();
            _writer.WriteNonEmptyNameArray(PropertyName, new string[] { null });

            const string expectedHash = "6JqdqL+bz8M=";
            string actualHash = _writer.GetHash();

            Assert.Equal(expectedHash, actualHash);
        }

        [Fact]
        public void GetHash_WithNoOtherChanges_ReturnsDefaultValue()
        {
            const string expectedHash = "AAAAAAAAAAA=";
            string actualHash = _writer.GetHash();

            Assert.Equal(expectedHash, actualHash);
        }

        [Fact]
        public void GetHash_WhenCalledOnCompleteObject_ReturnsHash()
        {
            _writer.WriteObjectStart();
            _writer.WriteObjectStart(PropertyName);
            _writer.WriteNameValue("b", 0);
            _writer.WriteNameValue("c", "d");
            _writer.WriteNameArray("e", new[] { "f", "g" });
            _writer.WriteObjectEnd();
            _writer.WriteObjectEnd();

            const string expectedHash = "heVVAmQ95DE=";
            string actualHash = _writer.GetHash();

            Assert.Equal(expectedHash, actualHash);
        }

        private void MakeReadOnly()
        {
            _writer.GetHash();
        }
    }
}
