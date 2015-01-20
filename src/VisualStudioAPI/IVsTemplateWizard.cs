using Microsoft.VisualStudio.TemplateWizard;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.VisualStudio
{
    [ComImport]
    [Guid("D6DEA71B-4A42-4B55-8A59-3191B91EF36E")]
    public interface IVsTemplateWizard : IWizard
    {
    }
}
