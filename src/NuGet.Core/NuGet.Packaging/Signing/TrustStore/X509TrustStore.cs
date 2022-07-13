// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// Represents the X.509 trust store that package signing and signed package verification will use.
    /// </summary>
    public static class X509TrustStore
    {
        private static IX509ChainFactory Instance;
        private static readonly object LockObject = new();

        /// <summary>
        /// Initializes the X.509 trust store for NuGet .NET SDK scenarios and logs details about the attempt.
        /// If initialization has already happened, a call to this method will have no effect.
        /// </summary>
        /// <param name="logger">A logger.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="logger" /> is <c>null</c>.</exception>
        public static void InitializeForDotNetSdk(ILogger logger)
        {
            _ = GetX509ChainFactory(logger, CreateX509ChainFactoryForDotNetSdk);
        }

        internal static IX509ChainFactory GetX509ChainFactory(ILogger logger)
        {
            return GetX509ChainFactory(logger, CreateX509ChainFactory);
        }

        private static IX509ChainFactory GetX509ChainFactory(ILogger logger, Func<ILogger, IX509ChainFactory> creator)
        {
            if (logger is null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            if (Instance is not null)
            {
                return Instance;
            }

            lock (LockObject)
            {
                if (Instance is not null)
                {
                    return Instance;
                }

                Instance = creator(logger);
            }

            return Instance;
        }

        internal static IX509ChainFactory CreateX509ChainFactoryForDotNetSdk(ILogger logger)
        {
            return CreateX509ChainFactoryForDotNetSdk(logger, fallbackCertificateBundleFile: null);
        }

        // Non-private for testing purposes only
        internal static IX509ChainFactory CreateX509ChainFactoryForDotNetSdk(ILogger logger, FileInfo fallbackCertificateBundleFile)
        {
#if NET5_0_OR_GREATER
            if (RuntimeEnvironmentHelper.IsLinux)
            {
                if (SystemCertificateBundleX509ChainFactory.TryCreate(
                    out SystemCertificateBundleX509ChainFactory systemBundleFactory))
                {
                    logger.LogInformation(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.ChainBuilding_UsingSystemCertificateBundle,
                            systemBundleFactory.FilePath));

                    return systemBundleFactory;
                }

                if (FallbackCertificateBundleX509ChainFactory.TryCreate(
                    out FallbackCertificateBundleX509ChainFactory fallbackBundleFactory,
                    fallbackCertificateBundleFile?.FullName))
                {
                    logger.LogInformation(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.ChainBuilding_UsingFallbackCertificateBundle,
                            fallbackBundleFactory.FilePath));

                    return fallbackBundleFactory;
                }

                logger.LogInformation(Strings.ChainBuilding_UsingNoCertificateBundle);

                return new NoCertificateBundleX509ChainFactory();
            }

            if (RuntimeEnvironmentHelper.IsMacOSX)
            {
                if (FallbackCertificateBundleX509ChainFactory.TryCreate(
                    out FallbackCertificateBundleX509ChainFactory fallbackBundleFactory,
                    fallbackCertificateBundleFile?.FullName))
                {
                    logger.LogInformation(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.ChainBuilding_UsingFallbackCertificateBundle,
                            fallbackBundleFactory.FilePath));

                    return fallbackBundleFactory;
                }

                logger.LogInformation(Strings.ChainBuilding_UsingNoCertificateBundle);

                return new NoCertificateBundleX509ChainFactory();
            }
#endif

            return CreateX509ChainFactory(logger);
        }

        // Non-private for testing purposes only
        internal static IX509ChainFactory CreateX509ChainFactory(ILogger logger)
        {
            logger.LogInformation(Strings.ChainBuilding_UsingDefaultTrustStore);

            return new DotNetDefaultTrustStoreX509ChainFactory();
        }

        // Only for testing
        internal static void SetX509ChainFactory(IX509ChainFactory chainFactory)
        {
            lock (LockObject)
            {
                Instance = chainFactory;
            }
        }
    }
}
