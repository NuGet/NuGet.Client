// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Configuration;

namespace NuGet.CommandLine
{
    [Command(typeof(NuGetCommand), "config", "ConfigCommandDesc", MaxArgs = 1,
            UsageSummaryResourceName = "ConfigCommandSummary", UsageExampleResourceName = "ConfigCommandExamples")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public class ConfigCommand : Command
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        private const string HttpPasswordKey = "http_proxy.password";

        [Option(typeof(NuGetCommand), "ConfigCommandSetDesc")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public Dictionary<string, string> Set { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "ConfigCommandAsPathDesc")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public bool AsPath
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            get;
            set;
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public override void ExecuteCommand()
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            if (Settings == null)
            {
                throw new InvalidOperationException(LocalizedResourceManager.GetString("Error_SettingsIsNull"));
            }

            var getKey = Arguments.FirstOrDefault();
            if (Set.Any())
            {
                foreach (var property in Set)
                {
                    if (string.IsNullOrEmpty(property.Value))
                    {
                        SettingsUtility.DeleteConfigValue(Settings, property.Key);
                    }
                    else
                    {
                        // Hack: Need a nicer way for the user to say encrypt this.
                        var encrypt = HttpPasswordKey.Equals(property.Key, StringComparison.OrdinalIgnoreCase);
                        SettingsUtility.SetConfigValue(Settings, property.Key, property.Value, encrypt);
                    }
                }
            }
            else if (!string.IsNullOrEmpty(getKey))
            {
                var value = SettingsUtility.GetConfigValue(Settings, getKey, isPath: AsPath);
                if (string.IsNullOrEmpty(value))
                {
                    Console.WriteError(LocalizedResourceManager.GetString("ConfigCommandKeyNotFound"), getKey);
                }
                else
                {
                    Console.WriteLine(value);
                }
            }
        }
    }
}
