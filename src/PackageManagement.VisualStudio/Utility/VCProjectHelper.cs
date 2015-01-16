using Microsoft.VisualStudio.VCProjectEngine;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.PackageManagement.VisualStudio
{
    public static class VCProjectHelper
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void AddFileToProject(object project, string filePath, string folderPath)
        {
            var vcProject = project as VCProject;
            if (vcProject != null)
            {
                if (String.IsNullOrEmpty(folderPath))
                {
                    vcProject.AddFile(filePath);
                }
                else
                {
                    var filter = GetFilter(vcProject, folderPath, createIfNotExists: true);
                    if (filter != null)
                    {
                        filter.AddFile(filePath);
                    }
                }
            }
        }

        /// <summary>
        /// Get the filter that is represented by the specified path
        /// </summary>
        private static VCFilter GetFilter(VCProject vcProject, string folderPath, bool createIfNotExists)
        {
            Debug.Assert(!String.IsNullOrEmpty(folderPath));

            string[] paths = folderPath.Split(Path.DirectorySeparatorChar);

            // recursively walks the folder path to get the last folder
            dynamic parent = vcProject;
            foreach (string path in paths)
            {
                VCFilter childFilter = null;
                foreach (VCFilter child in parent.Filters)
                {
                    if (child.Name.Equals(path, StringComparison.OrdinalIgnoreCase))
                    {
                        childFilter = child;
                        break;
                    }
                }

                if (childFilter == null)
                {
                    if (createIfNotExists)
                    {
                        // if a child folder doesn't already exist, create it
                        childFilter = parent.AddFilter(path);
                    }
                    else
                    {
                        return null;
                    }
                }
                parent = childFilter;
            }

            return (VCFilter)parent;
        }
    }
}
