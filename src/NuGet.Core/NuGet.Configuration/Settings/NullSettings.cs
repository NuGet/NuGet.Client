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

        public SettingSection GetSection(string sectionName) => null;

        public void AddOrUpdate(string sectionName, SettingItem item) => throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Resources.InvalidNullSettingsOperation, nameof(AddOrUpdate)));

        public void Remove(string sectionName, SettingItem item) => throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Resources.InvalidNullSettingsOperation, nameof(Remove)));

        public void SaveToDisk() { }

        //TODO: Remove deprecated methods
#pragma warning disable CS0618 // Type or member is obsolete

        [Obsolete("GetValue(...) is deprecated, please use GetSection(...) to interact with the setting values instead.")]
        public string GetValue(string section, string key, bool isPath = false) => string.Empty;

        [Obsolete("GetAllSubsections(...) is deprecated, please use GetSection(...) to interact with the setting values instead.")]
        public IReadOnlyList<string> GetAllSubsections(string section) => new List<string>().AsReadOnly();

        [Obsolete("GetSettingValues(...) is deprecated, please use GetSection(...) to interact with the setting values instead.")]
        public IList<SettingValue> GetSettingValues(string section, bool isPath = false) => new List<SettingValue>().AsReadOnly();

        [Obsolete("GetNestedValues(...) is deprecated, please use GetSection(...) to interact with the setting values instead.")]
        public IList<KeyValuePair<string, string>> GetNestedValues(string section, string subSection) => new List<KeyValuePair<string, string>>().AsReadOnly();

        [Obsolete("GetNestedSettingValues(...) is deprecated, please use GetSection(...) to interact with the setting values instead.")]
        public IReadOnlyList<SettingValue> GetNestedSettingValues(string section, string subSection) => new List<SettingValue>().AsReadOnly();

        [Obsolete("SetValue(...) is deprecated, please use AddOrUpdate(...) to add an item to a section or interact directly with the SettingItem you want.")]
        public void SetValue(string section, string key, string value) => throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Resources.InvalidNullSettingsOperation, nameof(SetValue)));

        [Obsolete("SetValues(...) is deprecated, please use AddOrUpdate(...) to add an item to a section or interact directly with the SettingItem you want.")]
        public void SetValues(string section, IReadOnlyList<SettingValue> values) => throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Resources.InvalidNullSettingsOperation, nameof(SetValues)));

        [Obsolete("UpdateSections(...) is deprecated, please use AddOrUpdate(...) to update an item in a section or interact directly with the SettingItem you want.")]
        public void UpdateSections(string section, IReadOnlyList<SettingValue> values) => throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Resources.InvalidNullSettingsOperation, nameof(UpdateSections)));

        [Obsolete("UpdateSubsections(...) is deprecated, please use AddOrUpdate(...) to update an item in a section or interact directly with the SettingItem you want.")]
        public void UpdateSubsections(string section, string subsection, IReadOnlyList<SettingValue> values) => throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Resources.InvalidNullSettingsOperation, nameof(UpdateSubsections)));

        [Obsolete("SetNestedValues(...) is deprecated, please use AddOrUpdate(...) to update an item in a section or interact directly with the SettingItem you want.")]
        public void SetNestedValues(string section, string subsection, IList<KeyValuePair<string, string>> values) => throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Resources.InvalidNullSettingsOperation, nameof(SetNestedValues)));

        [Obsolete("SetNestedSettingValues(...) is deprecated, please use AddOrUpdate(...) to update an item in a section or interact directly with the SettingItem you want.")]
        public void SetNestedSettingValues(string section, string subsection, IList<SettingValue> values) => throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Resources.InvalidNullSettingsOperation, nameof(SetNestedSettingValues)));

        [Obsolete("DeleteValue(...) is deprecated, please use Remove(...) with the item you want to remove from the setttings.")]
        public bool DeleteValue(string section, string key) => throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Resources.InvalidNullSettingsOperation, nameof(DeleteValue)));

        [Obsolete("DeleteSection(...) is deprecated,, please use Remove(...) with all the items in the section you want to remove from the setttings.")]
        public bool DeleteSection(string section) => throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Resources.InvalidNullSettingsOperation, nameof(DeleteSection)));
#pragma warning restore CS0618 // Type or member is obsolete
    }
}
