// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using NuGet.Common;

namespace NuGet.CommandLine
{
    /// <summary>
    /// Inspired from https://github.com/dotnet/sdk/blob/49d9b4148c4f65fd3f691186a4533375c3a83c97/src/Cli/dotnet/UILanguageOverride.cs#L9
    /// </summary>
    /// <remarks>nuget.exe does not flow other environment variables to child processes</remarks>
    internal static class UILanguageOverride
    {
        private const string NUGET_CLI_LANGUAGE = nameof(NUGET_CLI_LANGUAGE);
        private static ILogger Logger;

        public static void Setup(ILogger logger)
        {
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }
            Logger = logger;
            CultureInfo language = GetOverriddenUILanguage();
            if (language != null)
            {
                ApplyOverrideToCurrentProcess(language);
            }
        }

        private static void ApplyOverrideToCurrentProcess(CultureInfo language)
        {
            CultureInfo.DefaultThreadCurrentUICulture = language;
        }

        private static CultureInfo GetOverriddenUILanguage()
        {
            // NUGET_CLI_LANGUAGE=<culture name> is the main way for users to customize nuget.exe language.
            string nugetCliLanguage = Environment.GetEnvironmentVariable(NUGET_CLI_LANGUAGE);
            if (nugetCliLanguage != null)
            {
                try
                {
                    return new CultureInfo(nugetCliLanguage);
                }
                catch (CultureNotFoundException)
                {
                    Logger.LogError(string.Format(CultureInfo.CurrentCulture, NuGetResources.Error_InvalidCultureInfo, NUGET_CLI_LANGUAGE, nugetCliLanguage));
                }
            }

            return null;
        }
    }
}
