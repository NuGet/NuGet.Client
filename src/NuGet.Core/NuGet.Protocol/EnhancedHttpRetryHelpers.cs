// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Common;

namespace NuGet.Protocol
{
    internal class EnhancedHttpRetryHelper
    {
        private readonly IEnvironmentVariableReader _environmentVariableReader;
        internal const string ExperimentalRetryEnabledEnvVarName = "NUGET_ENABLE_EXPERIMENTAL_HTTP_RETRY";
        internal const string ExperimentalRetryTryCountEnvVarName = "NUGET_EXPERIMENTAL_MAX_NETWORK_TRY_COUNT";
        internal const string ExperimentalRetryDelayMsEnvVarName = "NUGET_EXPERIMENTAL_NETWORK_RETRY_DELAY_MILLISECONDS";

        public EnhancedHttpRetryHelper(IEnvironmentVariableReader environmentVariableReader)
        {
            _environmentVariableReader = environmentVariableReader ?? throw new ArgumentNullException(nameof(environmentVariableReader));
        }

        private bool? _enhancedHttpRetryIsEnabled = null;
        internal bool EnhancedHttpRetryEnabled
        {
            get
            {
                if (_enhancedHttpRetryIsEnabled == null)
                {
                    _enhancedHttpRetryIsEnabled = GetBooleanFromEnvironmentVariable(ExperimentalRetryEnabledEnvVarName, false, _environmentVariableReader);
                }
                return (bool)_enhancedHttpRetryIsEnabled;
            }
        }

        private int? _experimentalMaxNetworkTryCountValue = null;
        internal int ExperimentalMaxNetworkTryCount
        {
            get
            {
                if (_experimentalMaxNetworkTryCountValue == null)
                {
                    try
                    {
                        _experimentalMaxNetworkTryCountValue = GetIntFromEnvironmentVariable(ExperimentalRetryTryCountEnvVarName, 6, _environmentVariableReader);
                    }
                    catch (Exception) { }
                }
                return (int)_experimentalMaxNetworkTryCountValue;
            }
        }

        private int? _experimentalRetryDelayMillisecondsValue = null;
        internal int ExperimentalRetryDelayMilliseconds
        {
            get
            {
                if (_experimentalRetryDelayMillisecondsValue == null)
                {
                    _experimentalRetryDelayMillisecondsValue = GetIntFromEnvironmentVariable(ExperimentalRetryDelayMsEnvVarName, 1000, _environmentVariableReader);
                }
                return (int)_experimentalRetryDelayMillisecondsValue;
            }
        }

        private static int GetIntFromEnvironmentVariable(string variableName, int defaultValue, IEnvironmentVariableReader environmentVariableReader)
        {
            int retrievedValue = defaultValue;
            try
            {
                var variableValue = environmentVariableReader.GetEnvironmentVariable(variableName);
                if (!string.IsNullOrEmpty(variableValue))
                {
                    if (int.TryParse(variableValue, out int parsed))
                    {
                        retrievedValue = parsed;
                    }
                }
            }
            catch (Exception) { }
            return retrievedValue;
        }

        private static bool GetBooleanFromEnvironmentVariable(string variableName, bool defaultValue, IEnvironmentVariableReader environmentVariableReader)
        {
            bool retrievedValue = defaultValue;
            try
            {
                var variableValue = environmentVariableReader.GetEnvironmentVariable(variableName);
                if (!string.IsNullOrEmpty(variableValue))
                {
                    if (bool.TryParse(variableValue, out bool parsed))
                    {
                        retrievedValue = parsed;
                    }
                }
            }
            catch (Exception) { }
            return retrievedValue;
        }
    }
}
