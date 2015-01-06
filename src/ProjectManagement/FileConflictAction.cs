using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.ProjectManagement
{
    public enum FileConflictAction
    {
        PromptUser,
        Overwrite,
        OverwriteAll,
        Ignore,
        IgnoreAll
    }
}
