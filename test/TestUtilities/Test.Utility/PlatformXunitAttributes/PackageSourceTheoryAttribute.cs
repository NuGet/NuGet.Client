// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NuGet.Configuration;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

namespace NuGet.Test.Utility
{
    public sealed class PackageSourceTheoryAttribute : TheoryAttribute
    {
        private bool _ciOnly;

        public bool CIOnly
        {
            get => _ciOnly;
            set { _ciOnly = value; EvaluateSkip(); }
        }

        public string ConfigFile { get; set; } = TestSources.ConfigFile;

        public string Root { get; } = TestSources.GetConfigFileRoot();

        public PackageSourceTheoryAttribute()
        {
            EvaluateSkip();
        }

        private void EvaluateSkip()
        {
            if (_ciOnly && !XunitAttributeUtility.IsCI)
            {
                Skip = "This test only runs on the CI. To run it locally set the env var CI=true";
                return;
            }

            var fullPath = Path.Combine(Root, ConfigFile);
            if (!File.Exists(fullPath))
            {
                Skip = $"Required file does not exist: '{fullPath}'.";
                return;
            }

            Skip = null;
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class PackageSourceDataAttribute : DataAttribute
    {
        private static readonly ConcurrentDictionary<string, PackageSource[]> _cachedSources = new ConcurrentDictionary<string, PackageSource[]>();

        public ISet<string> SourceNames { get; }

        public PackageSourceDataAttribute(params string[] sourceNames)
        {
            SourceNames = new HashSet<string>(sourceNames, StringComparer.OrdinalIgnoreCase);
        }

        public override bool SupportsDiscoveryEnumeration() => true;

        public override ValueTask<IReadOnlyCollection<ITheoryDataRow>> GetData(MethodInfo testMethod, DisposalTracker disposalTracker)
        {
            var theoryAttribute = testMethod.GetCustomAttribute<PackageSourceTheoryAttribute>();
            if (theoryAttribute == null)
            {
                throw new ArgumentException("Theory attribute is required.");
            }

            var parameters = testMethod.GetParameters();
            if (parameters.Length != 1)
            {
                throw new ArgumentException("Invalid number of parameters. Should be 1.");
            }

            if (!string.IsNullOrEmpty(theoryAttribute.Skip))
            {
                return new ValueTask<IReadOnlyCollection<ITheoryDataRow>>(Array.Empty<ITheoryDataRow>());
            }

            if (!string.IsNullOrEmpty(Skip))
            {
                return new ValueTask<IReadOnlyCollection<ITheoryDataRow>>(Array.Empty<ITheoryDataRow>());
            }

            var root = TestSources.GetConfigFileRoot();
            var packageSources = GetTheorySources(root, theoryAttribute)
                .Where(s => s.IsEnabled && SourceNames.Contains(s.Name))
                .ToList();

            IReadOnlyCollection<ITheoryDataRow> result;

            if (parameters[0].ParameterType == typeof(PackageSource))
            {
                result = packageSources.Select(s => (ITheoryDataRow)new TheoryDataRow(s)).ToArray();
            }
            else if (parameters[0].ParameterType == typeof(string))
            {
                result = packageSources.Select(s => (ITheoryDataRow)new TheoryDataRow(s.Source)).ToArray();
            }
            else
            {
                throw new ArgumentException("Unsupported parameter type.");
            }

            return new ValueTask<IReadOnlyCollection<ITheoryDataRow>>(result);
        }

        private static PackageSource[] GetTheorySources(string root, PackageSourceTheoryAttribute theoryAttribute)
        {
            return _cachedSources.GetOrAdd(
                theoryAttribute.ConfigFile,
                configFile =>
                {
                    var settings = new Settings(root, configFile);
                    var provider = new PackageSourceProvider(settings);
                    return provider.LoadPackageSources().ToArray();
                });
        }
    }
}
