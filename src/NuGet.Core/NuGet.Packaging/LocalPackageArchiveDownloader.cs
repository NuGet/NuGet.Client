// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Core;

namespace NuGet.Packaging
{
    /// <summary>
    /// A package downloader for local archive packages.
    /// </summary>
    public sealed class LocalPackageArchiveDownloader : IPackageDownloader
    {
        private bool _isDisposed;
        private readonly ILogger _logger;
        private readonly string _packageFilePath;
        private readonly PackageIdentity _packageIdentity;
        private Lazy<PackageArchiveReader> _packageReader;
        private Lazy<FileStream> _sourceStream;

        /// <summary>
        /// Gets an asynchronous package content reader.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown if this object is disposed.</exception>
        public IAsyncPackageContentReader ContentReader
        {
            get
            {
                ThrowIfDisposed();

                return _packageReader.Value;
            }
        }

        /// <summary>
        /// Gets an asynchronous package core reader.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown if this object is disposed.</exception>
        public IAsyncPackageCoreReader CoreReader
        {
            get
            {
                ThrowIfDisposed();

                return _packageReader.Value;
            }
        }

        /// <summary>
        /// Initializes a new <see cref="LocalPackageArchiveDownloader" /> class.
        /// </summary>
        /// <param name="packageFilePath">A source package archive file path.</param>
        /// <param name="packageIdentity">A package identity.</param>
        /// <param name="logger">A logger.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="packageFilePath" />
        /// is either <c>null</c> or an empty string.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="packageIdentity" />
        /// is either <c>null</c> or an empty string.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="logger" />
        /// is either <c>null</c> or an empty string.</exception>
        public LocalPackageArchiveDownloader(
            string packageFilePath,
            PackageIdentity packageIdentity,
            ILogger logger)
        {
            if (string.IsNullOrEmpty(packageFilePath))
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.CurrentCulture, Strings.StringCannotBeNullOrEmpty, nameof(packageFilePath)),
                    nameof(packageFilePath));
            }

            if (packageIdentity == null)
            {
                throw new ArgumentNullException(nameof(packageIdentity));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            _packageFilePath = packageFilePath;
            _packageIdentity = packageIdentity;
            _logger = logger;
            _packageReader = new Lazy<PackageArchiveReader>(GetPackageReader);
            _sourceStream = new Lazy<FileStream>(GetSourceStream);
        }

        /// <summary>
        /// Disposes of this instance.
        /// </summary>
        public void Dispose()
        {
            if (!_isDisposed)
            {
                if (_packageReader.IsValueCreated)
                {
                    _packageReader.Value.Dispose();
                }

                if (_sourceStream.IsValueCreated)
                {
                    _sourceStream.Value.Dispose();
                }

                GC.SuppressFinalize(this);

                _isDisposed = true;
            }
        }

        /// <summary>
        /// Asynchronously copies a .nupkg to a target file path.
        /// </summary>
        /// <param name="destinationFilePath">The destination file path.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.
        /// The task result (<see cref="Task{TResult}.Result" />) returns a <see cref="bool" />
        /// indicating whether or not the copy was successful.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if this object is disposed.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="destinationFilePath" />
        /// is either <c>null</c> or empty.</exception>
        /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken" />
        /// is cancelled.</exception>
        public async Task<bool> CopyNupkgFileToAsync(string destinationFilePath, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(destinationFilePath))
            {
                throw new ArgumentException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.StringCannotBeNullOrEmpty,
                        nameof(destinationFilePath)),
                    nameof(destinationFilePath));
            }

            cancellationToken.ThrowIfCancellationRequested();

            using (var destination = new FileStream(
                destinationFilePath,
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 4096,
                useAsync: true))
            {
                await _sourceStream.Value.CopyToAsync(
                    destination,
                    bufferSize: 4096,
                    cancellationToken: cancellationToken);
            }

            return true;
        }

        /// <summary>
        /// Asynchronously gets a package hash.
        /// </summary>
        /// <param name="hashAlgorithm">The hash algorithm.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.
        /// The task result (<see cref="Task{TResult}.Result" />) returns a <see cref="string" />
        /// representing the package hash.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if this object is disposed.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="hashAlgorithm" />
        /// is either <c>null</c> or empty.</exception>
        /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken" />
        /// is cancelled.</exception>
        public Task<string> GetPackageHashAsync(string hashAlgorithm, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(hashAlgorithm))
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.CurrentCulture, Strings.StringCannotBeNullOrEmpty, nameof(hashAlgorithm)),
                    nameof(hashAlgorithm));
            }

            cancellationToken.ThrowIfCancellationRequested();

            _sourceStream.Value.Seek(0, SeekOrigin.Begin);

            var bytes = new CryptoHashProvider(hashAlgorithm).CalculateHash(_sourceStream.Value);
            var packageHash = Convert.ToBase64String(bytes);

            return Task.FromResult(packageHash);
        }

        private PackageArchiveReader GetPackageReader()
        {
            ThrowIfDisposed();

            _sourceStream.Value.Seek(0, SeekOrigin.Begin);

            return new PackageArchiveReader(_sourceStream.Value);
        }

        private FileStream GetSourceStream()
        {
            ThrowIfDisposed();

            return new FileStream(
               _packageFilePath,
               FileMode.Open,
               FileAccess.Read,
               FileShare.Read,
               bufferSize: 4096,
               useAsync: true);
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(LocalPackageArchiveDownloader));
            }
        }
    }
}