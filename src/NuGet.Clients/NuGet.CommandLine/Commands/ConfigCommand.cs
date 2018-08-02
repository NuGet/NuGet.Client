using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NuGet.Common;
using NuGet.Configuration;

namespace NuGet.CommandLine
{
    [Command(typeof(NuGetCommand), "config", "ConfigCommandDesc", MaxArgs = 1,
            UsageSummaryResourceName = "ConfigCommandSummary", UsageExampleResourceName = "ConfigCommandExamples")]
    public class ConfigCommand : Command
    {
        private const string HttpPasswordKey = "http_proxy.password";
        private readonly Dictionary<string, string> _setValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        [Option(typeof(NuGetCommand), "ConfigCommandSetDesc")]
        public Dictionary<string, string> Set
        {
            get { return _setValues; }
        }

        [Option(typeof(NuGetCommand), "ConfigCommandAsPathDesc")]
        public bool AsPath
        {
            get;
            set;
        }

        public override void ExecuteCommand()
        {
            if (Settings == null)
            {
                throw new InvalidOperationException(LocalizedResourceManager.GetString("Error_SettingsIsNull"));
            }

            string getKey = Arguments.FirstOrDefault();
            if (Set.Any())
            {
                foreach (var property in Set)
                {
                    if (String.IsNullOrEmpty(property.Value))
                    {
                        SettingsUtility.DeleteConfigValue(Settings, property.Key);
                    }
                    else
                    {
                        // Hack: Need a nicer way for the user to say encrypt this.
                        bool encrypt = HttpPasswordKey.Equals(property.Key, StringComparison.OrdinalIgnoreCase);
                        SettingsUtility.SetConfigValue(Settings, property.Key, property.Value, encrypt);
                    }
                }
            }
            else if (!String.IsNullOrEmpty(getKey))
            {
                string value = SettingsUtility.GetConfigValue(Settings, getKey, isPath: AsPath);
                if (String.IsNullOrEmpty(value))
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