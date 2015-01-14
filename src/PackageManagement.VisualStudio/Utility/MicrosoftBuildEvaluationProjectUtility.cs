using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Build.Evaluation;
using MicrosoftBuildEvaluationProject = Microsoft.Build.Evaluation.Project;

namespace NuGet.PackageManagement.VisualStudio
{
    internal static class MicrosoftBuildEvaluationProjectUtility
    {
        private const string ReferenceProjectItem = "Reference";

        internal static IEnumerable<Tuple<ProjectItem, AssemblyName>> GetAssemblyReferences(this MicrosoftBuildEvaluationProject msBuildEvaluationproject)
        {
            foreach (ProjectItem referenceProjectItem in msBuildEvaluationproject.GetItems(ReferenceProjectItem))
            {
                AssemblyName assemblyName = null;
                try
                {
                    assemblyName = new AssemblyName(referenceProjectItem.EvaluatedInclude);
                }
                catch (Exception exception)
                {
                    ExceptionHelper.WriteToActivityLog(exception);
                    // Swallow any exceptions we might get because of malformed assembly names
                }

                // We can't yield from within the try so we do it out here if everything was successful
                if (assemblyName != null)
                {
                    yield return Tuple.Create(referenceProjectItem, assemblyName);
                }
            }
        }
    }
}
