// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using NuGet.Common;

namespace NuGet.Protocol.Core.Types
{
    /// <summary>
    /// Base class for environment detection rules.
    /// </summary>
    internal abstract class EnvironmentDetectionRule
    {
        public string Name { get; }

        protected EnvironmentDetectionRule(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public abstract bool IsMatch(IEnvironmentVariableReader environmentVariableReader);
    }

    /// <summary>
    /// Rule that checks if any of the specified environment variables is set to "true" (case-insensitive).
    /// </summary>
    internal sealed class BooleanEnvironmentRule : EnvironmentDetectionRule
    {
        private readonly string[] _variableNames;

        public BooleanEnvironmentRule(string name, params string[] variableNames)
            : base(name)
        {
            _variableNames = variableNames ?? throw new ArgumentNullException(nameof(variableNames));
        }

        public override bool IsMatch(IEnvironmentVariableReader environmentVariableReader)
        {
            return _variableNames.Any(varName =>
            {
                string? value = environmentVariableReader.GetEnvironmentVariable(varName);
                return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
            });
        }
    }

    /// <summary>
    /// Rule that checks if all specified environment variables are present and non-empty.
    /// </summary>
    internal sealed class AllPresentEnvironmentRule : EnvironmentDetectionRule
    {
        private readonly string[] _variableNames;

        public AllPresentEnvironmentRule(string name, params string[] variableNames)
            : base(name)
        {
            _variableNames = variableNames ?? throw new ArgumentNullException(nameof(variableNames));
        }

        public override bool IsMatch(IEnvironmentVariableReader environmentVariableReader)
        {
            return _variableNames.All(varName =>
            {
                string? value = environmentVariableReader.GetEnvironmentVariable(varName);
                return !string.IsNullOrEmpty(value);
            });
        }
    }

    /// <summary>
    /// Rule that checks if any of the specified environment variables is present and non-empty.
    /// </summary>
    internal sealed class AnyPresentEnvironmentRule : EnvironmentDetectionRule
    {
        private readonly string[] _variableNames;

        public AnyPresentEnvironmentRule(string name, params string[] variableNames)
            : base(name)
        {
            _variableNames = variableNames ?? throw new ArgumentNullException(nameof(variableNames));
        }

        public override bool IsMatch(IEnvironmentVariableReader environmentVariableReader)
        {
            return _variableNames.Any(varName =>
            {
                string? value = environmentVariableReader.GetEnvironmentVariable(varName);
                return !string.IsNullOrEmpty(value);
            });
        }
    }
}
