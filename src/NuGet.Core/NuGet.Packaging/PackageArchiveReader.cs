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
        /// Underlying zip archive.
        /// </summary>
        protected ZipArchive Zip => _zipArchive;

        /// <summary>
        /// Stream underlying the ZipArchive. Used to do signature verification on a SignedPackageArchive.
        /// If this is null then we cannot perform signature verification.
        /// </summary>
        protected Stream ZipStream { get; set; }

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
            ZipStream = stream;
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
            ZipStream = stream;
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
                ZipStream = stream;
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

        public override async Task<IReadOnlyList<Signature>> GetSignaturesAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            if (ZipStream == null)
            {
                throw new SignatureException(Strings.SignedPackageUnableToAccessSignature);
            }

            var signatures = new List<Signature>();

            if (await IsSignedAsync(token))
            {
                var signatureEntry = Zip.GetEntry(_signingSpecifications.SignaturePath);
                using (var signatureEntryStream = signatureEntry.Open())
                {
#if IS_DESKTOP

                    var signature = Signature.Load(signatureEntryStream);
                    signatures.Add(signature);
#endif
                }
            }

            return signatures.AsReadOnly();
        }

        public override Task<bool> IsSignedAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            if(ZipStream == null)
            {
                throw new SignatureException(Strings.SignedPackageUnableToAccessSignature);
            }

            var isSigned = false;

#if IS_DESKTOP
            var signatureEntry = Zip.GetEntry(_signingSpecifications.SignaturePath);

            if (signatureEntry != null &&
                string.Equals(signatureEntry.Name, _signingSpecifications.SignaturePath, StringComparison.Ordinal))
            {
                isSigned = true;
            }
#endif
            return Task.FromResult(isSigned);
        }

        public override async Task ValidateIntegrityAsync(SignatureManifest signatureManifest, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            if (ZipStream == null)
            {
                throw new SignatureException(Strings.SignedPackageUnableToAccessSignature);
            }

            if (!await IsSignedAsync(token))
            {
                throw new SignatureException(Strings.SignedPackageNotSignedOnVerify);
            }

#if IS_DESKTOP
            using (var reader = new BinaryReader(ZipStream, _utf8Encoding, leaveOpen: true))
            {
                var hashAlgorithm = signatureManifest.HashAlgorithm.GetHashProvider();
                var expectedHash = Convert.FromBase64String(signatureManifest.HashValue);
                if (!SignedPackageArchiveUtility.VerifySignedZipIntegrity(reader, hashAlgorithm, expectedHash))
                {
                    throw new SignatureException(Strings.SignatureFailurePackageTampered);
                }
            }
#endif
        }

        public override Task<byte[]> GetArchiveHashAsync(HashAlgorithmName hashAlgorithm, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            if (ZipStream == null)
            {
                throw new SignatureException(Strings.SignedPackageUnableToAccessSignature);
            }

            ZipStream.Seek(offset: 0, origin: SeekOrigin.Begin);
            var hash = hashAlgorithm.GetHashProvider().ComputeHash(ZipStream, leaveStreamOpen: true);

            return Task.FromResult(hash);
        }
    }
}
