// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Common;

namespace NuGet.Configuration
{
    /// <summary>
    /// Machine wide settings based on the default machine wide config directory.
    /// </summary>
    public class XPlatMachineWideSetting : IMachineWideSettings
    {
        Lazy<IEnumerable<Settings>> _settings;

        public XPlatMachineWideSetting()
        {
            var baseDirectory = NuGetEnvironment.GetFolderPath(NuGetFolderPath.MachineWideConfigDirectory);
            _settings = new Lazy<IEnumerable<Settings>>(
                () => Configuration.Settings.LoadMachineWideSettings(baseDirectory));
        }

        public IEnumerable<Settings> Settings
        {
            get
            {
                return _settings.Value;
            }
        }
    }
}
