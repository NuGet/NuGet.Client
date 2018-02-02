// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Packaging.Signing;

namespace NuGet.Packaging
{
    /// <summary>
    /// Reads a development nupkg
    /// </summary>
    public class PackageArchiveReader : PackageReaderBase
    {
        private readonly ZipArchive _zipArchive;
        private readonly Encoding _utf8Encoding = new UTF8Encoding();
        private readonly SigningSpecifications _signingSpecifications = SigningSpecifications.V1;

        /// <summary>
        /// Signature specifications.
        /// </summary>
        protected SigningSpecifications SigningSpecifications => _signingSpecifications;

        /// <summary>
        /// Stream underlying the ZipArchive. Used to do signature verification on a SignedPackageArchive.
        /// If this is null then we cannot perform signature verification.
        /// </summary>
        protected Stream ZipReadStream { get; set; }

        /// <summary>
        /// Nupkg package reader
        /// </summary>
        /// <param name="stream">Nupkg data stream.</param>
        public PackageArchiveReader(Stream stream)
            : this(stream, false, DefaultFrameworkNameProvider.Instance, DefaultCompatibilityProvider.Instance)
        {
        }

        /// <summary>
        /// Nupkg package reader
        /// </summary>
        /// <param name="stream">Nupkg data stream.</param>
        /// <param name="frameworkProvider">Framework mapping provider for NuGetFramework parsing.</param>
        /// <param name="compatibilityProvider">Framework compatibility provider.</param>
        public PackageArchiveReader(Stream stream, IFrameworkNameProvider frameworkProvider, IFrameworkCompatibilityProvider compatibilityProvider)
            : this(stream, false)
        {
        }

        /// <summary>
        /// Nupkg package reader
        /// </summary>
        /// <param name="stream">Nupkg data stream.</param>
        /// <param name="leaveStreamOpen">If true the nupkg stream will not be closed by the zip reader.</param>
        public PackageArchiveReader(Stream stream, bool leaveStreamOpen)
            : this(new ZipArchive(stream, ZipArchiveMode.Read, leaveStreamOpen), DefaultFrameworkNameProvider.Instance, DefaultCompatibilityProvider.Instance)
        {
            ZipReadStream = stream;
        }

        /// <summary>
        /// Nupkg package reader
        /// </summary>
        /// <param name="stream">Nupkg data stream.</param>
        /// <param name="leaveStreamOpen">leave nupkg stream open</param>
        /// <param name="frameworkProvider">Framework mapping provider for NuGetFramework parsing.</param>
        /// <param name="compatibilityProvider">Framework compatibility provider.</param>
        public PackageArchiveReader(Stream stream, bool leaveStreamOpen, IFrameworkNameProvider frameworkProvider, IFrameworkCompatibilityProvider compatibilityProvider)
            : this(new ZipArchive(stream, ZipArchiveMode.Read, leaveStreamOpen), frameworkProvider, compatibilityProvider)
        {
            ZipReadStream = stream;
        }

        /// <summary>
        /// Nupkg package reader
        /// </summary>
        /// <param name="zipArchive">ZipArchive containing the nupkg data.</param>
        public PackageArchiveReader(ZipArchive zipArchive)
            : this(zipArchive, DefaultFrameworkNameProvider.Instance, DefaultCompatibilityProvider.Instance)
        {
        }

        /// <summary>
        /// Nupkg package reader
        /// </summary>
        /// <param name="zipArchive">ZipArchive containing the nupkg data.</param>
        /// <param name="frameworkProvider">Framework mapping provider for NuGetFramework parsing.</param>
        /// <param name="compatibilityProvider">Framework compatibility provider.</param>
        public PackageArchiveReader(ZipArchive zipArchive, IFrameworkNameProvider frameworkProvider, IFrameworkCompatibilityProvider compatibilityProvider)
            : base(frameworkProvider, compatibilityProvider)
        {
            _zipArchive = zipArchive ?? throw new ArgumentNullException(nameof(zipArchive));
        }

        public PackageArchiveReader(string filePath, IFrameworkNameProvider frameworkProvider = null, IFrameworkCompatibilityProvider compatibilityProvider = null)
            : base(frameworkProvider ?? DefaultFrameworkNameProvider.Instance, compatibilityProvider ?? DefaultCompatibilityProvider.Instance)
        {
            if (filePath == null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            // Since this constructor owns the stream, the responsibility falls here to dispose the stream of an
            // invalid .zip archive. If this constructor succeeds, the disposal of the stream is handled by the
            // disposal of this instance.
            Stream stream = null;
            try
            {
                stream = File.OpenRead(filePath);
                ZipReadStream = stream;
                _zipArchive = new ZipArchive(stream, ZipArchiveMode.Read);
            }
            catch
            {
                stream?.Dispose();
                throw;
            }
        }

        public override IEnumerable<string> GetFiles()
        {
            return _zipArchive.GetFiles();
        }

        public override IEnumerable<string> GetFiles(string folder)
        {
            return GetFiles().Where(f => f.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase));
        }

        public override Stream GetStream(string path)
        {
            Stream stream = null;
            path = path.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!string.IsNullOrEmpty(path))
            {
                stream = _zipArchive.OpenFile(path);
            }

            return stream;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _zipArchive.Dispose();
            }
        }

        public override IEnumerable<string> CopyFiles(
            string destination,
            IEnumerable<string> packageFiles,
            ExtractPackageFileDelegate extractFile,
            ILogger logger,
            CancellationToken token)
        {
            var filesCopied = new List<string>();

            foreach (var packageFile in packageFiles)
            {
                token.ThrowIfCancellationRequested();

                var entry = GetEntry(packageFile);

                var packageFileName = entry.FullName;
                // An entry in a ZipArchive could start with a '/' based on how it is zipped
                // Remove it if present
                if (packageFileName.StartsWith("/", StringComparison.Ordinal))
                {
                    packageFileName = packageFileName.Substring(1);
                }

                // ZipArchive always has forward slashes in them. By replacing them with DirectorySeparatorChar;
                // in windows, we get the windows-style path
                var normalizedPath = Uri.UnescapeDataString(packageFileName.Replace('/', Path.DirectorySeparatorChar));

                var targetFilePath = Path.Combine(destination, normalizedPath);
                if (!targetFilePath.StartsWith(destination, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                using (var stream = entry.Open())
                {
                    var copiedFile = extractFile(packageFileName, targetFilePath, stream);
                    if (copiedFile != null)
                    {
                        entry.UpdateFileTimeFromEntry(copiedFile, logger);
                        entry.UpdateFilePermissionsFromEntry(copiedFile, logger);

                        filesCopied.Add(copiedFile);
                    }
                }
            }

            return filesCopied;
        }

        public string ExtractFile(string packageFile, string targetFilePath, ILogger logger)
        {
            var entry = GetEntry(packageFile);
            var copiedFile = entry.SaveAsFile(targetFilePath, logger);
            return copiedFile;
        }

        private ZipArchiveEntry GetEntry(string packageFile)
        {
            return _zipArchive.LookupEntry(packageFile);
        }

        public IEnumerable<ZipFilePair> EnumeratePackageEntries(IEnumerable<string> packageFiles, string packageDirectory)
        {
            foreach (var packageFile in packageFiles)
            {
                var packageFileFullPath = Path.Combine(packageDirectory, packageFile);
                var entry = GetEntry(packageFile);
                yield return new ZipFilePair(packageFileFullPath, entry);
            }
        }

        public override async Task<Signature> GetSignatureAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            if (ZipReadStream == null)
            {
                throw new SignatureException(Strings.SignedPackageUnableToAccessSignature);
            }

            Signature signature = null;

            if (await IsSignedAsync(token))
            {
                using (var zip = new ZipArchive(ZipReadStream, ZipArchiveMode.Read, leaveOpen: true))
                {
                    var signatureEntry = zip.GetEntry(SigningSpecifications.SignaturePath);
                    using (var signatureEntryStream = signatureEntry.Open())
                    {
#if IS_DESKTOP
                        signature = Signature.Load(signatureEntryStream);
#endif
                    }
                }
            }

            return signature;
        }

        public override Task<bool> IsSignedAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            if (ZipReadStream == null)
            {
                throw new SignatureException(Strings.SignedPackageUnableToAccessSignature);
            }

            var isSigned = false;

#if IS_DESKTOP
            using (var zip = new ZipArchive(ZipReadStream, ZipArchiveMode.Read, leaveOpen: true))
            {
                var signatureEntry = zip.GetEntry(SigningSpecifications.SignaturePath);

                if (signatureEntry != null &&
                    string.Equals(signatureEntry.Name, SigningSpecifications.SignaturePath, StringComparison.Ordinal))
                {
                    isSigned = true;
                }
            }
#endif
            return Task.FromResult(isSigned);
        }

        public override async Task ValidateIntegrityAsync(SignatureContent signatureContent, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            if (ZipReadStream == null)
            {
                throw new SignatureException(Strings.SignedPackageUnableToAccessSignature);
            }

            if (!await IsSignedAsync(token))
            {
                throw new SignatureException(Strings.SignedPackageNotSignedOnVerify);
            }

#if IS_DESKTOP
            using (var reader = new BinaryReader(ZipReadStream, SigningSpecifications.Encoding, leaveOpen: true))
            using (var hashAlgorithm = signatureContent.HashAlgorithm.GetHashProvider())
            {
                var expectedHash = Convert.FromBase64String(signatureContent.HashValue);
                if (!SignedPackageArchiveUtility.VerifySignedPackageIntegrity(reader, hashAlgorithm, expectedHash))
                {
                    throw new SignatureException(NuGetLogCode.NU3008, Strings.SignaturePackageIntegrityFailure, GetIdentity());
                }
            }
#endif
        }

        public override Task<byte[]> GetArchiveHashAsync(HashAlgorithmName hashAlgorithmName, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            if (ZipReadStream == null)
            {
                throw new SignatureException(Strings.SignedPackageUnableToAccessSignature);
            }

            ZipReadStream.Seek(offset: 0, origin: SeekOrigin.Begin);

            using (var hashAlgorithm = hashAlgorithmName.GetHashProvider())
            {
                var hash = hashAlgorithm.ComputeHash(ZipReadStream, leaveStreamOpen: true);

                return Task.FromResult(hash);
            }
        }
    }
}