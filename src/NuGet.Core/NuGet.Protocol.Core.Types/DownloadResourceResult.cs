// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using NuGet.Packaging;

namespace NuGet.Protocol.Core.Types
{
    /// <summary>
    /// The result of <see cref="DownloadResource"/>.
    /// </summary>
    public class DownloadResourceResult : IDisposable
    {
        private readonly Stream _stream;
        private readonly PackageReaderBase _packageReader;
        private readonly string _packageSource;

        public DownloadResourceResult(DownloadResourceResultStatus status)
        {
            if (status == DownloadResourceResultStatus.Available)
            {
                throw new ArgumentException("A stream should be provided when the result is available.");
            }

            Status = status;
        }

        public DownloadResourceResult(Stream stream, string source)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            Status = DownloadResourceResultStatus.Available;
            _stream = stream;
            _packageSource = source;
        }

        public DownloadResourceResult(Stream stream)
            : this(stream, source: null)
        {
        }

        public DownloadResourceResult(Stream stream, PackageReaderBase packageReader, string source)
            : this(stream, source)
        {
            _packageReader = packageReader;
        }

        public DownloadResourceResult(Stream stream, PackageReaderBase packageReader)
            : this(stream, packageReader, source: null)
        {
        }

        public DownloadResourceResultStatus Status { get; }

        /// <summary>
        /// Gets the package <see cref="PackageStream"/>.
        /// </summary>
        public Stream PackageStream => _stream;

        /// <summary>
        /// Gets the source containing this package, if not from cache
        /// </summary>
        public string PackageSource => _packageSource;

        /// <summary>
        /// Gets the <see cref="PackageReaderBase"/> for the package.
        /// </summary>
        /// <remarks>This property can be null.</remarks>
        public PackageReaderBase PackageReader => _packageReader;

        public void Dispose()
        {
            _stream?.Dispose();
            _packageReader?.Dispose();
        }
    }
}
