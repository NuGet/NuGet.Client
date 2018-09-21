// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;

namespace NuGet.PackageManagement.VisualStudio
{
    public class BindingRedirectBehavior
    {
        private const string BindingRedirectsSection = "bindingRedirects";
        private const string SkipBindingRedirectsKey = "skip";
        private const string FailOnBindingRedirects = "successRequired";

        private readonly Configuration.ISettings _settings;

        public BindingRedirectBehavior(Configuration.ISettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            _settings = settings;
        }

        public bool IsSkipped
        {
            get
            {
                string settingsValue = _settings.GetValue(BindingRedirectsSection, SkipBindingRedirectsKey) ?? string.Empty;

                return IsSet(settingsValue, false); // Don't skip by default
            }

            set
            {
                _settings.SetValue(BindingRedirectsSection, SkipBindingRedirectsKey, value.ToString(CultureInfo.InvariantCulture));
            }
        }

        public bool FailOperations
        {
            get
            {
                string settingsValue = _settings.GetValue(BindingRedirectsSection, FailOnBindingRedirects) ?? string.Empty;

                return IsSet(settingsValue, false); // Ignore failures by default and just warn.
            }

            set
            {
                _settings.SetValue(BindingRedirectsSection, FailOnBindingRedirects, value.ToString(CultureInfo.InvariantCulture));
            }
        }

        private static bool IsSet(string value, bool defaultValue)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            value = value.Trim();

            bool boolResult;
            int intResult;

            var result = ((Boolean.TryParse(value, out boolResult) && boolResult) ||
                          (Int32.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out intResult) && (intResult == 1)));

            return result;
        }
    }
}
