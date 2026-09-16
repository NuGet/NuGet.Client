// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Credentials;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.CommandLine.XPlat.Commands.Stage
{
    internal sealed class StagePushCommandRunner
    {
        private const string ApiKeyEnvironmentVariableName = "NUGET_API_KEY";
        private const int FailureExitCode = 1;
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromMinutes(5);

        private readonly IEnvironmentVariableReader _environmentVariableReader;
        private readonly Func<IPackageSourceProvider, PackageSource, CancellationToken, Task<PackageStagingResourceV3?>> _getStagingResourceAsync;

        internal StagePushCommandRunner()
            : this(EnvironmentVariableWrapper.Instance, GetStagingResourceAsync)
        {
        }

        internal StagePushCommandRunner(IEnvironmentVariableReader environmentVariableReader)
            : this(environmentVariableReader, GetStagingResourceAsync)
        {
        }

        internal StagePushCommandRunner(
            IEnvironmentVariableReader environmentVariableReader,
            Func<IPackageSourceProvider, PackageSource, CancellationToken, Task<PackageStagingResourceV3?>> getStagingResourceAsync)
        {
            _environmentVariableReader = environmentVariableReader ?? throw new ArgumentNullException(nameof(environmentVariableReader));
            _getStagingResourceAsync = getStagingResourceAsync ?? throw new ArgumentNullException(nameof(getStagingResourceAsync));
        }

        internal Task<int> ExecuteCommandAsync(StagePushCommandArgs args)
        {
            ISettings settings = XPlatUtility.ProcessConfigFile(args.ConfigFile);
            var sourceProvider = new PackageSourceProvider(settings);
            DefaultCredentialServiceUtility.SetupDefaultCredentialService(args.Logger, !args.Interactive);

            return ExecuteCommandAsync(args, settings, sourceProvider);
        }

        internal async Task<int> ExecuteCommandAsync(
            StagePushCommandArgs args,
            ISettings settings,
            IPackageSourceProvider sourceProvider)
        {
            string packagePath = ValidatePackagePath(args.PackagePath);
            ValidateGroupId(args.GroupId);

            string source = ResolveSource(sourceProvider, args.Source);
            PackageSource packageSource = GetOrCreatePackageSource(sourceProvider, source);
            bool allowInsecureConnections = args.AllowInsecureConnections || packageSource.AllowInsecureConnections;

            if (packageSource.IsHttp && !packageSource.IsHttps)
            {
                if (!allowInsecureConnections)
                {
                    throw new ArgumentException(string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.StagePushCommand_Error_HttpSource,
                        packageSource.Source));
                }

                args.Logger.LogWarning(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.StagePushCommand_Warning_HttpSource,
                    packageSource.Source));
            }

            PackageStagingResourceV3? stagingResource = await _getStagingResourceAsync(
                sourceProvider,
                packageSource,
                args.CancellationToken);

            if (stagingResource is null)
            {
                throw new FatalProtocolException(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.StagePushCommand_Error_ResourceNotFound,
                    source));
            }

            string? apiKey = args.ApiKey;
            apiKey ??= _environmentVariableReader.GetEnvironmentVariable(ApiKeyEnvironmentVariableName);
            apiKey ??= SettingsUtility.GetApiKey(settings, stagingResource.SourceUri.AbsoluteUri, source);

            if (IsSymbolsPackage(packagePath))
            {
                await stagingResource.PushSymbolsAsync(
                    packagePath,
                    apiKey,
                    args.GroupId,
                    RequestTimeout,
                    allowInsecureConnections,
                    args.Logger,
                    args.CancellationToken);

                LogSymbolsStaged(args.Logger, packagePath);
                return ExitCodes.Success;
            }

            await stagingResource.PushPackageAsync(
                packagePath,
                apiKey,
                args.GroupId,
                RequestTimeout,
                allowInsecureConnections,
                args.Logger,
                args.CancellationToken);

            args.Logger.LogMinimal(string.Format(
                CultureInfo.CurrentCulture,
                Strings.StagePushCommand_PackageStaged,
                packagePath));

            if (args.NoSymbols)
            {
                return ExitCodes.Success;
            }

            string? symbolsPath = FindSymbolsPackage(packagePath);
            if (symbolsPath is null)
            {
                return ExitCodes.Success;
            }

            try
            {
                await stagingResource.PushSymbolsAsync(
                    symbolsPath,
                    apiKey,
                    args.GroupId,
                    RequestTimeout,
                    allowInsecureConnections,
                    args.Logger,
                    args.CancellationToken);
            }
            catch (FatalProtocolException ex)
            {
                return ReportSymbolsFailure(args.Logger, symbolsPath, ex.Message);
            }
            catch (HttpRequestException ex)
            {
                return ReportSymbolsFailure(args.Logger, symbolsPath, ex.Message);
            }

            LogSymbolsStaged(args.Logger, symbolsPath);
            return ExitCodes.Success;
        }

        internal static string? FindSymbolsPackage(string packagePath)
        {
            string packageStem = packagePath[..^".nupkg".Length];
            string snupkgPath = packageStem + ".snupkg";
            if (File.Exists(snupkgPath))
            {
                return snupkgPath;
            }

            string legacySymbolsPath = packageStem + ".symbols.nupkg";
            return File.Exists(legacySymbolsPath) ? legacySymbolsPath : null;
        }

        private static async Task<PackageStagingResourceV3?> GetStagingResourceAsync(
            IPackageSourceProvider sourceProvider,
            PackageSource packageSource,
            CancellationToken cancellationToken)
        {
            var sourceRepositoryProvider = new CachingSourceProvider(sourceProvider);
            SourceRepository sourceRepository = sourceRepositoryProvider.CreateRepository(packageSource);
            return await sourceRepository.GetResourceAsync<PackageStagingResourceV3>(cancellationToken);
        }

        private static string ValidatePackagePath(string packagePath)
        {
            if (string.IsNullOrWhiteSpace(packagePath))
            {
                throw new ArgumentException(Strings.StagePushCommand_Error_PackagePathRequired);
            }

            string fullPath = Path.GetFullPath(packagePath);
            if (!File.Exists(fullPath))
            {
                throw new ArgumentException(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.StagePushCommand_Error_PackageNotFound,
                    packagePath));
            }

            if (!IsPackage(fullPath) && !IsSymbolsPackage(fullPath))
            {
                throw new ArgumentException(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.StagePushCommand_Error_UnsupportedPackage,
                    packagePath));
            }

            return fullPath;
        }

        private static void ValidateGroupId(string? groupId)
        {
            if (groupId is not null && string.IsNullOrWhiteSpace(groupId))
            {
                throw new ArgumentException(Strings.StagePushCommand_Error_InvalidGroup);
            }
        }

        private static bool IsPackage(string packagePath)
        {
            return packagePath.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase)
                && !packagePath.EndsWith(".symbols.nupkg", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSymbolsPackage(string packagePath)
        {
            return packagePath.EndsWith(".snupkg", StringComparison.OrdinalIgnoreCase)
                || packagePath.EndsWith(".symbols.nupkg", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveSource(IPackageSourceProvider sourceProvider, string? source)
        {
            source ??= sourceProvider.DefaultPushSource;
            if (!string.IsNullOrEmpty(source))
            {
                source = sourceProvider.ResolveAndValidateSource(source);
            }

            if (string.IsNullOrEmpty(source))
            {
                throw new ArgumentException(Strings.StagePushCommand_Error_MissingSource);
            }

            return source;
        }

        private static PackageSource GetOrCreatePackageSource(IPackageSourceProvider sourceProvider, string source)
        {
            return sourceProvider.LoadPackageSources()
                .FirstOrDefault(packageSource =>
                    packageSource.IsEnabled
                    && string.Equals(source, packageSource.Source, StringComparison.OrdinalIgnoreCase))
                ?? new PackageSource(source);
        }

        private static void LogSymbolsStaged(ILogger logger, string symbolsPath)
        {
            logger.LogMinimal(string.Format(
                CultureInfo.CurrentCulture,
                Strings.StagePushCommand_SymbolsStaged,
                symbolsPath));
        }

        private static int ReportSymbolsFailure(ILogger logger, string symbolsPath, string errorMessage)
        {
            logger.LogError(string.Format(
                CultureInfo.CurrentCulture,
                Strings.StagePushCommand_SymbolsFailed,
                symbolsPath,
                errorMessage));

            return FailureExitCode;
        }
    }
}
