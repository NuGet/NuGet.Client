// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using Microsoft.Test.Apex.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Win32;

namespace NuGet.Tests.Apex
{
    [Export(typeof(FeatureFlagTestService))]
    public class FeatureFlagTestService : VisualStudioTestService
    {
        private string? _featureFlagPath;
        private object? _originalValue;
        private RegistryValueKind _originalValueKind;
        private bool _originalKeyExists;

        public void CaptureOverride(string featureFlagName)
        {
            UIInvoke(() =>
            {
                if (_featureFlagPath != null)
                {
                    throw new InvalidOperationException("A feature flag override has already been captured.");
                }

                using var root = VSRegistry.RegistryRoot(__VsLocalRegistryType.RegType_UserSettings);
                var path = @"FeatureFlags\" + featureFlagName;
                using var key = root.OpenSubKey(path);
                _originalKeyExists = key != null;
                _originalValue = key?.GetValue("Value", null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                if (key != null && _originalValue != null)
                {
                    _originalValueKind = key.GetValueKind("Value");
                }
                _featureFlagPath = path;
            });
        }

        public void RestoreOverride()
        {
            UIInvoke(() =>
            {
                if (_featureFlagPath == null)
                {
                    throw new InvalidOperationException("No feature flag override has been captured.");
                }

                using var root = VSRegistry.RegistryRoot(__VsLocalRegistryType.RegType_UserSettings, writable: true);
                if (_originalValue != null)
                {
                    using var key = root.CreateSubKey(_featureFlagPath);
                    key.SetValue("Value", _originalValue, _originalValueKind);
                }
                else
                {
                    // SetFeatureFlag persists a local override even when none existed before the test.
                    var removeKey = false;
                    using (var key = root.OpenSubKey(_featureFlagPath, writable: true))
                    {
                        key?.DeleteValue("Value", throwOnMissingValue: false);
                        removeKey = !_originalKeyExists && key != null && key.ValueCount == 0 && key.SubKeyCount == 0;
                    }

                    if (removeKey)
                    {
                        root.DeleteSubKey(_featureFlagPath);
                    }
                }
                _featureFlagPath = null;
            });
        }
    }
}
