// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using NuGet.ProjectManagement;
using MicrosoftBuildEvaluationProject = Microsoft.Build.Evaluation.Project;

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
                    ExceptionHelper.WriteErrorToActivityLog(exception);
                    // Swallow any exceptions we might get because of malformed assembly names
                }

                // We can't yield from within the try so we do it out here if everything was successful
                if (assemblyName != null)
                {
                    yield return Tuple.Create(referenceProjectItem, assemblyName);
                }
            }
        }

        internal static void AddImportStatement(MicrosoftBuildEvaluationProject msBuildProject, string targetsPath, ImportLocation location)
        {
            if (msBuildProject.Xml.Imports == null
                ||
                msBuildProject.Xml.Imports.All(import => !targetsPath.Equals(import.Project, StringComparison.OrdinalIgnoreCase)))
            {
                ProjectImportElement pie = null;

                if (location == ImportLocation.Top)
                {
                    pie = msBuildProject.Xml.CreateImportElement(targetsPath);

                    msBuildProject.Xml.InsertBeforeChild(pie, msBuildProject.Xml.FirstChild);
                }
                else if (location == ImportLocation.Bottom)
                {
                    pie = msBuildProject.Xml.AddImport(targetsPath);
                }
                else if (location == ImportLocation.ExtensionSettings)
                {
                    ProjectImportGroupElement importGroup = null;

                    //Find ExtensionSettings ImportGroup
                    if (msBuildProject.Xml.ImportGroups != null)
                    {
                        importGroup = msBuildProject.Xml.ImportGroups.First(group => group.Label.Equals("ExtensionSettings", StringComparison.OrdinalIgnoreCase));
                    }


                    if(importGroup == null && msBuildProject.Xml.Imports != null)
                    {
                        //Find <Import Project="$(VCTargetsPath)\Microsoft.Cpp.props" />

                        var where = msBuildProject.Xml.Imports.First(import => import.Project.Equals("$(VCTargetsPath)\\Microsoft.Cpp.props", StringComparison.OrdinalIgnoreCase));

                        if(where != null)
                        {
                            // Insert <ImportGroup Label="ExtensionSettings">
                            importGroup = msBuildProject.Xml.CreateImportGroupElement();
                            importGroup.Label = "ExtensionSettings";

                            msBuildProject.Xml.InsertAfterChild(importGroup, where);
                        }
                    }


                    if(importGroup != null)
                    {
                        pie = importGroup.AddImport(targetsPath);
                    }
                }
                else if (location == ImportLocation.Shared)
                {
                    //VC++ Shared
                    ProjectImportGroupElement importGroup = null;

                        
                    if (msBuildProject.Xml.ImportGroups != null)
                    {
                        //Find Shared ImportGroup
                        importGroup = msBuildProject.Xml.ImportGroups.First(group => group.Label.Equals("Shared", StringComparison.OrdinalIgnoreCase));


                        if(importGroup == null)
                        {
                            //Find ExtensionSettings ImportGroup
                            ProjectElement where = msBuildProject.Xml.ImportGroups.First(group => group.Label.Equals("ExtensionSettings", StringComparison.OrdinalIgnoreCase));

                            if(where == null && msBuildProject.Xml.Imports != null)
                            {
                                //Find <Import Project="$(VCTargetsPath)\Microsoft.Cpp.props" />

                                where = msBuildProject.Xml.Imports.First(import => import.Project.Equals("$(VCTargetsPath)\\Microsoft.Cpp.props", StringComparison.OrdinalIgnoreCase));

                            }


                            if(where != null)
                            {
                                //Add Shared ImportGroup
                                importGroup = msBuildProject.Xml.CreateImportGroupElement();
                                importGroup.Label = "Shared";

                                msBuildProject.Xml.InsertAfterChild(importGroup, where);
                            }
                        }
                    }


                    if(importGroup != null)
                    {
                        pie = importGroup.AddImport(targetsPath);
                        pie.Label = "Shared";
                    }
                }
                else if (location == ImportLocation.ExtensionTargets)
                {
                    //Find ExtensionTargets ImportGroup
                    var importGroup = msBuildProject.Xml.ImportGroups.First(group => group.Label.Equals("ExtensionTargets", StringComparison.OrdinalIgnoreCase));


                    if(importGroup == null && msBuildProject.Xml.Imports != null)
                    {
                        //Find <Import Project="$(VCTargetsPath)\Microsoft.Cpp.targets" />
                        var where = msBuildProject.Xml.Imports.First(import => import.Project.Equals("$(VCTargetsPath)\\Microsoft.Cpp.targets", StringComparison.OrdinalIgnoreCase));

                        if(where != null)
                        {
                            importGroup = msBuildProject.Xml.CreateImportGroupElement();
                            importGroup.Label = "ExtensionTargets";

                            msBuildProject.Xml.InsertAfterChild(importGroup, where);
                        }
                    }

                    if(importGroup != null)
                    {
                        pie = importGroup.AddImport(targetsPath);
                    }
                }

                if (pie == null)
                    return;

                pie.Condition = "Exists('" + targetsPath + "')";

                AddEnsureImportedTarget(msBuildProject, targetsPath);
                msBuildProject.ReevaluateIfNecessary();
            }
        }

        /// <summary>
        /// Removes the Import element from the project file.
        /// </summary>
        /// <param name="msBuildProject">The project file.</param>
        /// <param name="targetsPath">The path to the imported file.</param>
        internal static void RemoveImportStatement(MicrosoftBuildEvaluationProject msBuildProject, string targetsPath)
        {
            if (msBuildProject.Xml.Imports != null)
            {
                // search for this import statement and remove it
                var importElement = msBuildProject.Xml.Imports.FirstOrDefault(
                    import => targetsPath.Equals(import.Project, StringComparison.OrdinalIgnoreCase));

                if (importElement != null)
                {
                    importElement.Parent.RemoveChild(importElement);
                    RemoveEnsureImportedTarget(msBuildProject, targetsPath);
                    msBuildProject.ReevaluateIfNecessary();
                }
            }
        }

        private static void AddEnsureImportedTarget(MicrosoftBuildEvaluationProject msBuildProject, string targetsPath)
        {
            // get the target
            var targetElement = msBuildProject.Xml.Targets.FirstOrDefault(
                target => target.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase));

            // if the target does not exist, create the target
            if (targetElement == null)
            {
                targetElement = msBuildProject.Xml.AddTarget(targetName);

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

        private static void RemoveEnsureImportedTarget(MicrosoftBuildEvaluationProject msBuildProject, string targetsPath)
        {
            var targetElement = msBuildProject.Xml.Targets.FirstOrDefault(
                target => string.Equals(target.Name, targetName, StringComparison.OrdinalIgnoreCase));
            if (targetElement == null)
            {
                return;
            }

            string errorCondition = "!Exists('" + targetsPath + "')";
            var taskElement = targetElement.Tasks.FirstOrDefault(
                task => string.Equals(task.Condition, errorCondition, StringComparison.OrdinalIgnoreCase));
            if (taskElement == null)
            {
                return;
            }

            taskElement.Parent.RemoveChild(taskElement);
            if (targetElement.Tasks.Count == 0)
            {
                targetElement.Parent.RemoveChild(targetElement);
            }
        }
    }
}
