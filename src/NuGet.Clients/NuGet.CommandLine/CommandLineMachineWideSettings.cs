// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using NuGet.Common;

namespace NuGet.CommandLine
{
    [Export(typeof(Configuration.IMachineWideSettings))]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public class CommandLineMachineWideSettings : Configuration.IMachineWideSettings
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        Lazy<Configuration.ISettings> _settings;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public CommandLineMachineWideSettings()
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            var baseDirectory = NuGetEnvironment.GetFolderPath(NuGetFolderPath.MachineWideConfigDirectory);
            _settings = new Lazy<Configuration.ISettings>(
                () => Configuration.Settings.LoadMachineWideSettings(baseDirectory));
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public Configuration.ISettings Settings => _settings.Value;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    }
}
