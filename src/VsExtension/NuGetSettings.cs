using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.PackageManagement.UI;

namespace NuGetVSExtension
{
    /// <summary>
    /// The user settings that are persisted in suo file
    /// </summary>
    [Serializable]
    class NuGetSettings
    {
        public Dictionary<string, UserSettings> WindowSettings { get; private set; }

        public NuGetSettings()
        {
            WindowSettings = new Dictionary<string, UserSettings>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
