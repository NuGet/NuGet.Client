using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.ProjectModel;

namespace NuGet.Commands
{
    public class DotnetCliToolRestoreResult
    {
        /// <summary>
        /// Full path to write file to
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Result file
        /// </summary>
        public DotnetCliToolFile ToolFile { get; }

        public DotnetCliToolRestoreResult(string path, DotnetCliToolFile toolFile)
        {
            Path = path;
            ToolFile = toolFile;
        }
    }
}
