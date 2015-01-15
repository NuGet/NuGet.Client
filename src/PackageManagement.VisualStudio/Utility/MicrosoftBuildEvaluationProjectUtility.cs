using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Build.Evaluation;
using MicrosoftBuildEvaluationProject = Microsoft.Build.Evaluation.Project;
using System.Linq;
using Microsoft.Build.Construction;
using System.Globalization;
using NuGet.ProjectManagement;

namespace NuGet.PackageManagement.VisualStudio
{
    internal static class MicrosoftBuildEvaluationProjectUtility
    {
        private const string ReferenceProjectItem = "Reference";
        private const string targetName = "EnsureNuGetPackageBuildImports";

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

        internal static void AddImportStatement(MicrosoftBuildEvaluationProject project, string targetsPath, ImportLocation location)
        {
            if (project.Xml.Imports == null ||
                project.Xml.Imports.All(import => !targetsPath.Equals(import.Project, StringComparison.OrdinalIgnoreCase)))
            {
                ProjectImportElement pie = project.Xml.AddImport(targetsPath);
                pie.Condition = "Exists('" + targetsPath + "')";
                if (location == ImportLocation.Top)
                {
                    // There's no public constructor to create a ProjectImportElement directly.
                    // So we have to cheat by adding Import at the end, then remove it and insert at the beginning
                    pie.Parent.RemoveChild(pie);
                    project.Xml.InsertBeforeChild(pie, project.Xml.FirstChild);
                }

                AddEnsureImportedTarget(project, targetsPath);
                project.ReevaluateIfNecessary();
            }
        }

        private static void AddEnsureImportedTarget(MicrosoftBuildEvaluationProject buildProject, string targetsPath)
        {
            // get the target
            var targetElement = buildProject.Xml.Targets.FirstOrDefault(
                target => target.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase));

            // if the target does not exist, create the target
            if (targetElement == null)
            {
                targetElement = buildProject.Xml.AddTarget(targetName);

                // PrepareForBuild is used here because BeforeBuild does not work for VC++ projects.
                targetElement.BeforeTargets = "PrepareForBuild";

                var propertyGroup = targetElement.AddPropertyGroup();
                propertyGroup.AddProperty("ErrorText", CommonResources.EnsureImportedMessage);
            }

            var errorTask = targetElement.AddTask("Error");
            errorTask.Condition = "!Exists('" + targetsPath + "')";
            var errorText = string.Format(
                CultureInfo.InvariantCulture,
                @"$([System.String]::Format('$(ErrorText)', '{0}'))",
                targetsPath);
            errorTask.SetParameter("Text", errorText);
        }

    }
}
