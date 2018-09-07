using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.PackageManagement.UI
{
    public class PackageManagementFormat
    {
        private const string PackageManagementSection = "packageManagement";
        private const string DoNotShowPackageManagementSelectionKey = "disabled";
        private const string DefaultPackageManagementFormatKey = "format";
        private const string PackageReferenceDoc = "https://aka.ms/packagereferencesupport";

        private readonly Configuration.ISettings _settings;

        // keep track of current value for selected package format
        private int SelectedPackageFormat = -1;

        // keep track of current value for shwo dialog checkbox
        private bool? ShowDialogValue;

        public PackageManagementFormat(Configuration.ISettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            _settings = settings;

            PackageRefUri = new Uri(PackageReferenceDoc);
        }

        public Uri PackageRefUri { get; private set; }

        public List<string> ProjectNames { get; set; }

        public string PackageFormatSelectorLabel
        {
            get
            {
                if (ProjectNames.Count == 1)
                {
                    return string.Format(Resources.Text_PackageFormatSelection, ProjectNames.First());
                }
                else
                {
                    return string.Format(Resources.Text_PackageFormatSelection_Solution);
                }
            }
        }

        public bool IsSolution
        {
            get
            {
                return ProjectNames.Count > 1;
            }
        }

        public bool Enabled
        {
            get
            {
                if (ShowDialogValue.HasValue)
                {
                    return ShowDialogValue.Value;
                }

                string settingsValue = _settings.GetValue(PackageManagementSection, DoNotShowPackageManagementSelectionKey) ?? string.Empty;
                ShowDialogValue = IsSet(settingsValue, false);
                return ShowDialogValue.Value;
            }

            set
            {
                ShowDialogValue = value;
            }
        }

        public int SelectedPackageManagementFormat
        {
            get
            {
                if (SelectedPackageFormat != -1)
                {
                    return SelectedPackageFormat;
                }

                string settingsValue = _settings.GetValue(PackageManagementSection, DefaultPackageManagementFormatKey) ?? string.Empty;
                SelectedPackageFormat = IsSet(settingsValue, 0);
                return SelectedPackageFormat;
            }

            set
            {
                SelectedPackageFormat = value;
            }
        }

        public void ApplyChanges()
        {
            _settings.SetValue(PackageManagementSection, DefaultPackageManagementFormatKey, SelectedPackageFormat.ToString(CultureInfo.InvariantCulture));
            _settings.SetValue(PackageManagementSection, DoNotShowPackageManagementSelectionKey, ShowDialogValue.Value.ToString(CultureInfo.InvariantCulture));
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

        private static int IsSet(string value, int defaultValue)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            value = value.Trim();

            var result = Int32.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture);

            return result;
        }
    }
}
