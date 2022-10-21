// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;

namespace NuGet.Commands
{
    /// <summary>Represents an environment variable to control culture in NuGet CLIs</summary>
    // This type is public because it's the only way to call it from NuGet.CommandLine.XPlat (dotnet.exe) and NuGet.CommandLine (nuget.exe) projects
    // without introducing a new assembly
    public class LanguageEnvironmentVariable
    {
        public string VariableName { get; }
        public Func<string, CultureInfo> GeneratorFunc { get; }
        public Func<CultureInfo, string> EnvVarValueFunc { get; }

        public LanguageEnvironmentVariable(string variableName, Func<string, CultureInfo> generatorFunc, Func<CultureInfo, string> valueFunc)
        {
            if (string.IsNullOrWhiteSpace(variableName) || string.Empty.Equals(variableName, StringComparison.InvariantCultureIgnoreCase))
            {
                throw new ArgumentException(Strings.ArgumentCannotBeNullOrEmpty, nameof(variableName));
            }
            if (generatorFunc == null)
            {
                throw new ArgumentNullException(nameof(generatorFunc));
            }
            if (valueFunc == null)
            {
                throw new ArgumentNullException(nameof(valueFunc));
            }

            VariableName = variableName;
            GeneratorFunc = generatorFunc;
            EnvVarValueFunc = valueFunc;
        }

        public static CultureInfo GetCultureFromName(string envvarValue) => new(envvarValue);

        public static CultureInfo GetCultureFromLCID(string envvarValue)
        {
            int lcid;
            if (int.TryParse(envvarValue, out lcid))
            {
                return new CultureInfo(lcid);
            }

            return null;
        }

        public static string CultureToName(CultureInfo culture) => culture?.Name ?? throw new ArgumentNullException(nameof(culture));

        public static string CultureToLCID(CultureInfo culture) => culture?.LCID.ToString(CultureInfo.InvariantCulture) ?? throw new ArgumentNullException(nameof(culture));
    }
}
