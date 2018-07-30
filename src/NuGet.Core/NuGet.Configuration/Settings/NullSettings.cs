// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace NuGet.Configuration
{
    public class NullSettings : ISettings
    {
        public event EventHandler SettingsChanged = delegate { };

        public static NullSettings Instance { get; } = new NullSettings();

        public SettingsSection GetSection(string sectionName)
        {
            return null;
        }

        public bool CreateSection(SettingsSection section)
        {
            throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Resources.InvalidNullSettingsOperation, nameof(CreateSection)));
        }

        public bool SetItemInSection(string sectionName, SettingsItem item)
        {
            throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Resources.InvalidNullSettingsOperation, nameof(SetItemInSection)));
        }

        public void Save() { }

        public IEnumerable<string> GetConfigFilePaths()
        {
            return new List<string>();
        }

        public IEnumerable<string> GetConfigRoots()
        {
            return new List<string>();
        }

        public bool CreateSection(SettingsSection section, bool isBatchOperation = false)
        {
            throw new NotImplementedException();
        }

        public bool SetItemInSection(string sectionName, SettingsItem item, bool isBatchOperation = false)
        {
            throw new NotImplementedException();
        }
    }
}
