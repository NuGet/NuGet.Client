// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.RuntimeModel;

namespace NuGet.ProjectModel
{
    /// <summary>
    /// Generates a hash from an object graph.
    ///
    /// This is non-private only to facilitate unit testing.
    /// </summary>
    public sealed class HashObjectWriter : IObjectWriter, IDisposable
    {
        private const int DefaultBufferSize = 4096;

        private static readonly SerializedPackageReferenceComparer<CentralPackageVersion> CentralPackageVersionComparer =
            new(static item => item.Value.Name, static value => value.VersionRange);
        private static readonly SerializedPackageReferenceComparer<PrunePackageReference> PruningReferenceComparer =
            new(static item => item.Key, static value => value.VersionRange);

        private readonly byte[] _buffer;
        private readonly IHashFunction _hashFunc;
        private bool _isDisposed;
        private bool _isReadOnly;
        private int _nestLevel;
        private readonly CircularMemoryStream _stream;
        private readonly StreamWriter _streamWriter;
        private readonly JsonTextWriter _writer;
        private StringBuilder? _serializationBuffer;
        private IEnumerable<KeyValuePair<string, PackageSpec>>? _serializationScope;
        private bool _serializationScopeSeeded;
        private Dictionary<SerializedPackageReferenceKey<CentralPackageVersion>, SerializedPackageReferences<CentralPackageVersion>>? _centralSections;
        private Dictionary<SerializedPackageReferenceKey<PrunePackageReference>, SerializedPackageReferences<PrunePackageReference>>? _pruningSections;

        /// <summary>
        /// Creates a new instance with the provide hash function.
        /// </summary>
        /// <param name="hashFunc">An <see cref="IHashFunction"/> instance.  Throws if <see langword="null" />.</param>
        public HashObjectWriter(IHashFunction hashFunc)
            : this(hashFunc, serializationScope: null)
        {
        }

        internal HashObjectWriter(IHashFunction hashFunc, IEnumerable<KeyValuePair<string, PackageSpec>>? serializationScope)
        {
            if (hashFunc == null)
            {
                throw new ArgumentNullException(nameof(hashFunc));
            }

            _buffer = new byte[DefaultBufferSize];
            _hashFunc = hashFunc;
            _stream = new CircularMemoryStream(_buffer);
            _streamWriter = new StreamWriter(_stream);
            _writer = new JsonTextWriter(_streamWriter);
            _serializationScope = serializationScope;

            _stream.OnFlush += OnFlush;
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _serializationBuffer = null;
                _serializationScope = null;
                _centralSections = null;
                _pruningSections = null;
                _stream.OnFlush -= OnFlush;

                _hashFunc.Dispose();
                _writer.Close();
                _streamWriter.Dispose();
                _stream.Dispose();

                _isDisposed = true;
            }
        }

        public void WriteObjectStart()
        {
            ThrowIfDisposed();
            ThrowIfReadOnly();

            _writer.WriteStartObject();

            ++_nestLevel;
        }

        public void WriteObjectStart(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            ThrowIfDisposed();
            ThrowIfReadOnly();

            _writer.WritePropertyName(name);
            _writer.WriteStartObject();

            ++_nestLevel;
        }

        public void WriteObjectEnd()
        {
            ThrowIfDisposed();
            ThrowIfReadOnly();

            if (_nestLevel == 0)
            {
                throw new InvalidOperationException();
            }

            _writer.WriteEndObject();

            --_nestLevel;
        }

        public void WriteNameValue(string name, int value)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            ThrowIfDisposed();
            ThrowIfReadOnly();

            _writer.WritePropertyName(name);
            _writer.WriteValue(value);
        }

        public void WriteNameValue(string name, bool value)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            ThrowIfDisposed();
            ThrowIfReadOnly();

            _writer.WritePropertyName(name);
            _writer.WriteValue(value);
        }

        public void WriteNameValue(string name, string? value)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            ThrowIfDisposed();
            ThrowIfReadOnly();

            _writer.WritePropertyName(name);
            _writer.WriteValue(value);
        }

        internal void WriteNameRawValue(string name, string json)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            ThrowIfDisposed();
            ThrowIfReadOnly();

            // Keep cached text on the existing encoding path so hash-update boundaries stay unchanged.
            _writer.WritePropertyName(name);
            _writer.WriteRawValue(json);
        }

        internal string SerializeObject<T>(KeyValuePair<string, T>[] references, Action<IObjectWriter, KeyValuePair<string, T>[]> writeValues)
        {
            ThrowIfDisposed();
            ThrowIfReadOnly();

            StringBuilder buffer = _serializationBuffer ??= new StringBuilder();
            buffer.Clear();
            using var textWriter = new StringWriter(buffer, CultureInfo.InvariantCulture);
            using var jsonWriter = new JsonTextWriter(textWriter);
            using var writer = new JsonObjectWriter(jsonWriter);
            writer.WriteObjectStart();
            writeValues(writer, references);
            writer.WriteObjectEnd();
            return buffer.ToString();
        }

        internal SerializedPackageReferences<CentralPackageVersion>? GetOrSerializeCentralPackageVersions(
            KeyValuePair<string, CentralPackageVersion>[] references,
            Action<IObjectWriter, KeyValuePair<string, CentralPackageVersion>[]> writeValues)
        {
            return GetOrSerialize(references, writeValues, CentralPackageVersionComparer, ref _centralSections);
        }

        internal SerializedPackageReferences<PrunePackageReference>? GetOrSerializePackagesToPrune(
            KeyValuePair<string, PrunePackageReference>[] references,
            Action<IObjectWriter, KeyValuePair<string, PrunePackageReference>[]> writeValues)
        {
            return GetOrSerialize(references, writeValues, PruningReferenceComparer, ref _pruningSections);
        }

        private SerializedPackageReferences<T>? GetOrSerialize<T>(
            KeyValuePair<string, T>[] references,
            Action<IObjectWriter, KeyValuePair<string, T>[]> writeValues,
            SerializedPackageReferenceComparer<T> comparer,
            ref Dictionary<SerializedPackageReferenceKey<T>, SerializedPackageReferences<T>>? cache)
        {
            ThrowIfDisposed();
            ThrowIfReadOnly();
            if (!comparer.TryCreateKey(references, out SerializedPackageReferenceKey<T> key))
            {
                return null;
            }
            SeedSerializationScope();
            cache ??= new Dictionary<SerializedPackageReferenceKey<T>, SerializedPackageReferences<T>>(comparer);
            if (cache.TryGetValue(key, out SerializedPackageReferences<T>? existing))
            {
                return existing;
            }

            var serialized = new SerializedPackageReferences<T>(key, SerializeObject(references, writeValues));
            cache.Add(key, serialized);
            return serialized;
        }

        private void SeedSerializationScope()
        {
            if (_serializationScopeSeeded)
            {
                return;
            }
            _serializationScopeSeeded = true;
            if (_serializationScope == null)
            {
                return;
            }

            foreach (var project in _serializationScope)
            {
                foreach (var framework in project.Value.TargetFrameworks.NoAllocEnumerate())
                {
                    AddCachedSection(framework.GetCachedCentralPackageVersions(), CentralPackageVersionComparer, ref _centralSections);
                    AddCachedSection(framework.GetCachedPackagesToPrune(), PruningReferenceComparer, ref _pruningSections);
                }
            }
        }

        private static void AddCachedSection<T>(
            SerializedPackageReferences<T>? section,
            SerializedPackageReferenceComparer<T> comparer,
            ref Dictionary<SerializedPackageReferenceKey<T>, SerializedPackageReferences<T>>? cache)
        {
            if (section == null)
            {
                return;
            }

            // The section's content key is immutable, even if its original model's inputs changed.
            cache ??= new Dictionary<SerializedPackageReferenceKey<T>, SerializedPackageReferences<T>>(comparer);
            if (!cache.ContainsKey(section.Key))
            {
                cache.Add(section.Key, section);
            }
        }

        public void WriteNameArray(string name, IEnumerable<string> values)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            ThrowIfDisposed();
            ThrowIfReadOnly();

            _writer.WritePropertyName(name);
            _writer.WriteStartArray();

            foreach (string value in values)
            {
                _writer.WriteValue(value);
            }

            _writer.WriteEndArray();
        }

        public void WriteNonEmptyNameArray(string name, IEnumerable<string> values)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            ThrowIfDisposed();
            ThrowIfReadOnly();

            // Manually enumerate the IEnumerable so we only write the name
            // when there are corresponding values and avoid potentially expensive
            // multiple enumeration.
            var enumerator = values.NoAllocEnumerate().GetEnumerator();
            if (!enumerator.MoveNext())
            {
                return;
            }

            _writer.WritePropertyName(name);
            _writer.WriteStartArray();
            _writer.WriteValue(enumerator.Current);
            while (enumerator.MoveNext())
            {
                _writer.WriteValue(enumerator.Current);
            }

            _writer.WriteEndArray();
        }

        /// <summary>
        /// Gets the hash for the object.
        ///
        /// Once GetHash is called, no further writing is allowed.
        /// </summary>
        /// <returns>A hash of the object.</returns>
        public string GetHash()
        {
            ThrowIfDisposed();

            if (!_isReadOnly)
            {
                _writer.Flush();

                _isReadOnly = true;
            }

            return _hashFunc.GetHash();
        }

        public void WriteArrayStart(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            ThrowIfDisposed();
            ThrowIfReadOnly();

            _writer.WritePropertyName(name);
            _writer.WriteStartArray();

            ++_nestLevel;
        }

        public void WriteArrayEnd()
        {
            ThrowIfDisposed();
            ThrowIfReadOnly();

            if (_nestLevel == 0)
            {
                throw new InvalidOperationException();
            }

            _writer.WriteEndArray();

            --_nestLevel;
        }

        private void OnFlush(object? sender, ArraySegment<byte> bytes)
        {
            if (bytes.Count > 0)
            {
                _hashFunc.Update(bytes.Array!, bytes.Offset, bytes.Count);
            }
        }

        private void ThrowIfReadOnly()
        {
            if (_isReadOnly)
            {
                throw new InvalidOperationException();
            }
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(HashObjectWriter));
            }
        }
    }
}
