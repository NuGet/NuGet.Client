// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Signing;
using NuGet.Protocol;

namespace NuGet.Commands
{
    /// <summary>
    /// Command Runner used to run the business logic for nuget sign command
    /// </summary>
    public class SignCommandRunner : ISignCommandRunner
    {
        public async Task<int> ExecuteCommandAsync(SignArgs signArgs)
        {
            // resolve path into multiple packages if needed.
            IEnumerable<string> packagesToSign = signArgs.PackagePaths.SelectMany(packagePath =>
            {
                IEnumerable<string> packages = LocalFolderUtility.ResolvePackageFromPath(packagePath);
                LocalFolderUtility.EnsurePackageFileExists(packagePath, packages);
                return packages;
            });

            var success = true;

            X509Certificate2 cert = null;
            try
            {
                cert = await GetCertificateAsync(signArgs);
            }
            catch (Exception e)
            {
                success = false;
                ExceptionUtilities.LogException(e, signArgs.Logger);
                if (e is System.Security.Cryptography.CryptographicException ce)
                {
                    signArgs.Logger.LogError(ce.HResult.ToString(CultureInfo.InvariantCulture));
                }
            }

            if (success)
            {
                signArgs.Logger.LogInformation(Environment.NewLine);
                signArgs.Logger.LogInformation(Strings.SignCommandDisplayCertificate);
                signArgs.Logger.LogInformation(CertificateUtility.X509Certificate2ToString(cert, HashAlgorithmName.SHA256));

                if (!string.IsNullOrEmpty(signArgs.Timestamper))
                {
                    signArgs.Logger.LogInformation(Strings.SignCommandDisplayTimestamper);
                    signArgs.Logger.LogInformation(signArgs.Timestamper);
                }

                if (!string.IsNullOrEmpty(signArgs.OutputDirectory))
                {
                    signArgs.Logger.LogInformation(Strings.SignCommandOutputPath);
                    signArgs.Logger.LogInformation(signArgs.OutputDirectory);
                }

                using (var signRequest = new AuthorSignPackageRequest(cert, signArgs.SignatureHashAlgorithm, signArgs.TimestampHashAlgorithm))
                {
#if NET5_0_OR_GREATER
                    if (signArgs.TrustAnchorPaths != null && signArgs.TrustAnchorPaths.Count > 0)
                    {
                        foreach (string anchorPath in signArgs.TrustAnchorPaths)
                        {
#if NET9_0_OR_GREATER
                            signRequest.AdditionalTrustAnchors.Add(X509CertificateLoader.LoadCertificateFromFile(anchorPath));
#else
                            signRequest.AdditionalTrustAnchors.Add(new X509Certificate2(anchorPath));
#endif
                        }
                    }

                    if (signArgs.TrustAnchorFingerprints != null && signArgs.TrustAnchorFingerprints.Count > 0)
                    {
                        X509Certificate2 rootFromChain = GetChainRoot(cert, signRequest.AdditionalCertificates);
                        if (rootFromChain == null)
                        {
                            throw new SignCommandException(
                                LogMessage.CreateError(NuGetLogCode.NU3001,
                                Strings.SignCommandTrustAnchorChainBuildFailed));
                        }

                        foreach (string fingerprint in signArgs.TrustAnchorFingerprints)
                        {
                            if (!CertificateUtility.TryDeduceHashAlgorithm(fingerprint, out HashAlgorithmName fingerprintAlgorithm) ||
                                fingerprintAlgorithm == HashAlgorithmName.Unknown)
                            {
                                throw new SignCommandException(
                                    LogMessage.CreateError(NuGetLogCode.NU3001,
                                    string.Format(CultureInfo.CurrentCulture, Strings.SignCommandInvalidTrustAnchorFingerprint, fingerprint)));
                            }

                            string rootFingerprint = CertificateUtility.GetHashString(rootFromChain, fingerprintAlgorithm);
                            if (string.Equals(rootFingerprint, fingerprint, StringComparison.OrdinalIgnoreCase))
                            {
                                signRequest.AdditionalTrustAnchors.Add(rootFromChain);
                            }
                            else
                            {
                                throw new SignCommandException(
                                    LogMessage.CreateError(NuGetLogCode.NU3001,
                                    string.Format(CultureInfo.CurrentCulture, Strings.SignCommandTrustAnchorNotFound, fingerprint)));
                            }
                        }
                    }
#endif
                    return await ExecuteCommandAsync(
                        packagesToSign,
                        signRequest,
                        signArgs.Timestamper,
                        signArgs.Logger,
                        signArgs.OutputDirectory,
                        signArgs.Overwrite,
                        signArgs.Token);
                }
            }

            return success ? 0 : 1;
        }

        public async Task<int> ExecuteCommandAsync(
            IEnumerable<string> packagesToSign,
            SignPackageRequest signPackageRequest,
            string timestamper,
            ILogger logger,
            string outputDirectory,
            bool overwrite,
            CancellationToken token)
        {
            var success = true;

            try
            {
                SigningUtility.Verify(signPackageRequest, logger);
            }
            catch (Exception e)
            {
                success = false;
                ExceptionUtilities.LogException(e, logger);
            }

            if (success)
            {
                var signatureProvider = GetSignatureProvider(timestamper);

                foreach (var packagePath in packagesToSign)
                {
                    // Set the output of the signing operation to a temp file because signing cannot be done in place.
                    var tempPackageFile = new FileInfo(Path.GetTempFileName());

                    try
                    {
                        string outputPath;

                        if (string.IsNullOrEmpty(outputDirectory))
                        {
                            outputPath = packagePath;
                        }
                        else
                        {
                            outputPath = Path.Combine(outputDirectory, Path.GetFileName(packagePath));
                        }

                        using (var options = SigningOptions.CreateFromFilePaths(
                            packagePath,
                            tempPackageFile.FullName,
                            overwrite,
                            signatureProvider,
                            logger))
                        {
                            await SigningUtility.SignAsync(options, signPackageRequest, token);
                        }

                        if (tempPackageFile.Length > 0)
                        {
                            FileUtility.Replace(tempPackageFile.FullName, outputPath);
                        }
                        else
                        {
                            throw new SignatureException(Strings.Error_UnableToSignPackage);
                        }
                    }
                    catch (Exception e)
                    {
                        success = false;
                        ExceptionUtilities.LogException(e, logger);
                    }
                    finally
                    {
                        FileUtility.Delete(tempPackageFile.FullName);
                    }
                }
            }

            if (success)
            {
                logger.LogInformation(Strings.SignCommandSuccess);
            }

            return success ? 0 : 1;
        }

        private static ISignatureProvider GetSignatureProvider(string timestamper)
        {
            Rfc3161TimestampProvider timestampProvider = null;

            if (!string.IsNullOrEmpty(timestamper))
            {
                timestampProvider = new Rfc3161TimestampProvider(new Uri(timestamper));
            }

            return new X509SignatureProvider(timestampProvider);
        }

        private static async Task<X509Certificate2> GetCertificateAsync(SignArgs signArgs)
        {
            var certFindOptions = new CertificateSourceOptions()
            {
                CertificatePath = signArgs.CertificatePath,
                CertificatePassword = signArgs.CertificatePassword,
                Fingerprint = signArgs.CertificateFingerprint,
                StoreLocation = signArgs.CertificateStoreLocation,
                StoreName = signArgs.CertificateStoreName,
                SubjectName = signArgs.CertificateSubjectName,
                NonInteractive = signArgs.NonInteractive,
                PasswordProvider = signArgs.PasswordProvider,
                Token = signArgs.Token
            };

            // get matching certificates
            var matchingCertCollection = await CertificateProvider.GetCertificatesAsync(certFindOptions);

            if (matchingCertCollection.Count > 1)
            {
#if IS_DESKTOP
                if (signArgs.NonInteractive || !RuntimeEnvironmentHelper.IsWindows)
                {
                    // if on non-windows os or in non interactive mode - display the certs and error out
                    signArgs.Logger.LogInformation(CertificateUtility.X509Certificate2CollectionToString(matchingCertCollection, HashAlgorithmName.SHA256));
                    throw new SignCommandException(
                        LogMessage.CreateError(NuGetLogCode.NU3001,
                        string.Format(CultureInfo.CurrentCulture, Strings.SignCommandMultipleCertException,
                        nameof(SignArgs.CertificateFingerprint))));
                }
                else
                {
                    // Else launch UI to select
                    matchingCertCollection = X509Certificate2UI.SelectFromCollection(
                        FilterCodeSigningCertificates(matchingCertCollection),
                        Strings.SignCommandDialogTitle,
                        Strings.SignCommandDialogMessage,
                        X509SelectionFlag.SingleSelection);
                }
#else
                // if on non-windows os or in non interactive mode - display and error out
                signArgs.Logger.LogError(CertificateUtility.X509Certificate2CollectionToString(matchingCertCollection, HashAlgorithmName.SHA256));

                throw new SignCommandException(
                    LogMessage.CreateError(NuGetLogCode.NU3001,
                    string.Format(CultureInfo.CurrentCulture, Strings.SignCommandMultipleCertException,
                    nameof(SignArgs.CertificateFingerprint))));
#endif
            }

            if (matchingCertCollection.Count == 0)
            {
                throw new SignCommandException(
                    LogMessage.CreateError(NuGetLogCode.NU3001,
                    Strings.SignCommandNoCertException));
            }

            return matchingCertCollection[0];
        }

#if IS_DESKTOP
        private static X509Certificate2Collection FilterCodeSigningCertificates(X509Certificate2Collection matchingCollection)
        {
            var filteredCollection = new X509Certificate2Collection();

            foreach (var certificate in matchingCollection)
            {
                if (CertificateUtility.IsValidForPurposeFast(certificate, Oids.CodeSigningEku))
                {
                    filteredCollection.Add(certificate);
                }
            }

            return filteredCollection;
        }
#endif

#if NET5_0_OR_GREATER
        /// <summary>
        /// Builds a preliminary certificate chain from the signing certificate and returns
        /// the root element. The chain may not fully validate (e.g., untrusted root), but
        /// the chain elements will still be populated with the discovered certificates.
        /// </summary>
        private static X509Certificate2 GetChainRoot(X509Certificate2 signingCert, X509Certificate2Collection additionalCertificates)
        {
            using (var chain = new X509Chain())
            {
                chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;

                if (additionalCertificates != null)
                {
                    chain.ChainPolicy.ExtraStore.AddRange(additionalCertificates);
                }

                // Build may return false (untrusted root), but chain elements are still populated
                chain.Build(signingCert);

                if (chain.ChainElements.Count > 0)
                {
                    // The last element in the chain is the root CA
                    return new X509Certificate2(chain.ChainElements[chain.ChainElements.Count - 1].Certificate);
                }

                return null;
            }
        }
#endif
    }
}
