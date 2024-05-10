// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using NuGet.Commands;

namespace NuGet.CommandLine
{
    [Command(typeof(NuGetCommand), "sources", "SourcesCommandDescription", UsageSummaryResourceName = "SourcesCommandUsageSummary",
        MinArgs = 0, MaxArgs = 1)]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public class SourcesCommand : Command
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        [Option(typeof(NuGetCommand), "SourcesCommandNameDescription")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string Name { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "SourcesCommandSourceDescription", AltName = "src")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string Source { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "SourcesCommandUserNameDescription")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string Username { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "SourcesCommandPasswordDescription")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string Password { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "SourcesCommandStorePasswordInClearTextDescription")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public bool StorePasswordInClearText { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "SourcesCommandValidAuthenticationTypesDescription")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string ValidAuthenticationTypes { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "SourcesCommandProtocolVersionDescription")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string ProtocolVersion { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "SourcesCommandFormatDescription")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public SourcesListFormat Format { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member


#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public override void ExecuteCommand()
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            if (SourceProvider == null)
            {
                throw new InvalidOperationException(LocalizedResourceManager.GetString("Error_SourceProviderIsNull"));
            }

            SourcesAction action = SourcesAction.None;
            var actionArg = Arguments.FirstOrDefault();
            if (string.IsNullOrEmpty(actionArg))
            {
                action = SourcesAction.List;
            }
            else
            {
                if (!Enum.TryParse<SourcesAction>(actionArg, ignoreCase: true, out action))
                {
                    Console.WriteLine(LocalizedResourceManager.GetString("SourcesCommandUsageSummary"));
                }
            }

            switch (action)
            {
                case SourcesAction.Add:
                    var addArgs = new AddSourceArgs() { Name = Name, Source = Source, Username = Username, Password = Password, StorePasswordInClearText = StorePasswordInClearText, ValidAuthenticationTypes = ValidAuthenticationTypes, Configfile = ConfigFile, ProtocolVersion = ProtocolVersion };
                    AddSourceRunner.Run(addArgs, () => Console);
                    break;
                case SourcesAction.Update:
                    var updateSourceArgs = new UpdateSourceArgs() { Name = Name, Source = Source, Username = Username, Password = Password, StorePasswordInClearText = StorePasswordInClearText, ValidAuthenticationTypes = ValidAuthenticationTypes, Configfile = ConfigFile, ProtocolVersion = ProtocolVersion };
                    UpdateSourceRunner.Run(updateSourceArgs, () => Console);
                    break;
                case SourcesAction.Remove:
                    var removeSourceArgs = new RemoveSourceArgs() { Name = Name, Configfile = ConfigFile };
                    RemoveSourceRunner.Run(removeSourceArgs, () => Console);
                    break;
                case SourcesAction.Disable:
                    var disableSourceArgs = new DisableSourceArgs() { Name = Name, Configfile = ConfigFile };
                    DisableSourceRunner.Run(disableSourceArgs, () => Console);
                    break;
                case SourcesAction.Enable:
                    var enableSourceArgs = new EnableSourceArgs() { Name = Name, Configfile = ConfigFile };
                    EnableSourceRunner.Run(enableSourceArgs, () => Console);
                    break;
                case SourcesAction.List:
                    if (Format == SourcesListFormat.None)
                    {
                        Format = SourcesListFormat.Detailed;
                    }
                    var listSourceArgs = new ListSourceArgs() { Configfile = ConfigFile, Format = Format.ToString() };
                    ListSourceRunner.Run(listSourceArgs, () => Console);
                    break;
            }
        }
    }
}
