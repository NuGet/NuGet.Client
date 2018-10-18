using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.PackageManagement.UI.Models
{
    internal class LicenseFileText : IText
    {
        public LicenseFileText(string text)
        {
            Text = text;
        }

        public string Text { get; }
    }
}
