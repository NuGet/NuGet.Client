// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using NuGet.Common;

namespace NuGet.Commands
{
    /// <summary>Set a CLI culture using environment variables</summary>
    /// <remarks>
    /// This type is public because it's the only way to call it from NuGet.CommandLine.XPlat (dotnet.exe) and NuGet.CommandLine (nuget.exe) projects
    /// without introducing a new assembly
    /// </remarks>
    public class CLILanguageOverrider
    {
        private readonly ILogger _logger;
        private readonly IEnumerable<LanguageEnvironmentVariable> _envVarDefs;
        private readonly bool _flowToProcess;

        public CLILanguageOverrider(ILogger logger, IEnumerable<LanguageEnvironmentVariable> varsToProbe, bool flowEnvvarsToChildProcess = false)
        {
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }
            _logger = logger;

            if (varsToProbe == null)
            {
                throw new ArgumentNullException(nameof(varsToProbe));
            }

            _envVarDefs = varsToProbe;
            _flowToProcess = flowEnvvarsToChildProcess;
        }

        public void Setup()
        {
            CultureInfo language = GetOverriddenUILanguage();
            if (language != null)
            {
                ApplyOverrideToCurrentProcess(language);
                if (_flowToProcess)
                {
                    FlowOverrideToChildProcesses(language);
                }
            }
        }

        private static void ApplyOverrideToCurrentProcess(CultureInfo language)
        {
            CultureInfo.DefaultThreadCurrentUICulture = language;
        }

        private void FlowOverrideToChildProcesses(CultureInfo language)
        {
            foreach (LanguageEnvironmentVariable langEnvvar in _envVarDefs)
            {
                // Do not override any environment variables that are already set as we do not want to clobber a more granular setting with our global setting.
                SetIfNotAlreadySet(langEnvvar.VariableName, langEnvvar.EnvVarValueFunc(language));
            }
        }

        private CultureInfo GetOverriddenUILanguage()
        {
            CultureInfo culture = null;
            string envvarValue;
            foreach (LanguageEnvironmentVariable langEnvvar in _envVarDefs)
            {
                envvarValue = Environment.GetEnvironmentVariable(langEnvvar.VariableName);
                if (!string.IsNullOrEmpty(envvarValue))
                {
                    try
                    {
                        culture = null;
                        culture = langEnvvar.GeneratorFunc(envvarValue);
                    }
                    catch (CultureNotFoundException)
                    {
                        _logger.LogError(string.Format(CultureInfo.CurrentCulture, Strings.Error_InvalidCultureInfo, langEnvvar.VariableName, envvarValue));
                    }
                }

                if (culture != null)
                {
                    return culture;
                }
            }

            return null;
        }

        private static void SetIfNotAlreadySet(string environmentVariableName, string value)
        {
            string currentValue = Environment.GetEnvironmentVariable(environmentVariableName);
            if (currentValue == null)
            {
                Environment.SetEnvironmentVariable(environmentVariableName, value, EnvironmentVariableTarget.Process);
            }
        }
    }
}
