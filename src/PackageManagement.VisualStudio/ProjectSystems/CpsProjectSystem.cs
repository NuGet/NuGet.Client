using NuGet.ProjectManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using EnvDTEProject = EnvDTE.Project;
#if VS10 || VS11 || VS12
using NuGetVS = NuGet.VisualStudio12;
#endif

#if VS14
using NuGetVS = NuGet.VisualStudio14;
#endif

namespace NuGet.PackageManagement.VisualStudio
{
    public abstract class CpsProjectSystem : VSMSBuildNuGetProjectSystem
    {
        protected CpsProjectSystem(EnvDTEProject envDTEProject, INuGetProjectContext nuGetProjectContext)
            : base(envDTEProject, nuGetProjectContext)
        {
        }

        protected override void AddGacReference(string name)
        {
            // Native & JS projects don't know about GAC
        }

        public override void AddImport(string targetPath, ImportLocation location)
        {
            // For VS 2012 or above, the operation has to be done inside the Writer lock
            if (String.IsNullOrEmpty(targetPath))
            {
                throw new ArgumentNullException(CommonResources.Argument_Cannot_Be_Null_Or_Empty, "targetPath");
            }
            var root = EnvDTEProjectUtility.GetFullPath(EnvDTEProject);
            string relativeTargetPath = PathUtility.GetRelativePath(PathUtility.EnsureTrailingSlash(root), targetPath);
            if (VSVersionHelper.IsVisualStudio2012)
            {
                EnvDTEProjectUtility.DoWorkInWriterLock(EnvDTEProject, buildProject => MicrosoftBuildEvaluationProjectUtility.AddImportStatement(buildProject, relativeTargetPath, location));
                EnvDTEProjectUtility.Save(EnvDTEProject);
            }
            else
            {
                AddImportStatementForVS2013(location, relativeTargetPath);
            }      
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void AddImportStatementForVS2013(ImportLocation location, string relativeTargetPath)
        {
            NuGetVS.ProjectHelper.DoWorkInWriterLock(
                EnvDTEProject,
                VsHierarchyUtility.ToVsHierarchy(EnvDTEProject),
                buildProject => MicrosoftBuildEvaluationProjectUtility.AddImportStatement(buildProject, relativeTargetPath, location));

            // notify the project system of the change
            UpdateImportStamp(EnvDTEProject, true);
        }

        public override void RemoveImport(string targetPath)
        {
           if (String.IsNullOrEmpty(targetPath))
           {
               throw new ArgumentNullException(CommonResources.Argument_Cannot_Be_Null_Or_Empty, "targetPath");
           }
           var root = EnvDTEProjectUtility.GetFullPath(EnvDTEProject);
            // For VS 2012 or above, the operation has to be done inside the Writer lock
           string relativeTargetPath = PathUtility.GetRelativePath(PathUtility.EnsureTrailingSlash(root), targetPath);
           if (VSVersionHelper.IsVisualStudio2012)
           {
               EnvDTEProjectUtility.DoWorkInWriterLock(EnvDTEProject, buildProject => MicrosoftBuildEvaluationProjectUtility.RemoveImportStatement(buildProject, relativeTargetPath));
               EnvDTEProjectUtility.Save(EnvDTEProject);
           }
           else
           {
               RemoveImportStatementForVS2013(relativeTargetPath);
           }
        }

        // IMPORTANT: The NoInlining is required to prevent CLR from loading VisualStudio12.dll assembly while running 
        // in VS2010 and VS2012
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void RemoveImportStatementForVS2013(string relativeTargetPath)
        {
            NuGetVS.ProjectHelper.DoWorkInWriterLock(
                EnvDTEProject,
                VsHierarchyUtility.ToVsHierarchy(EnvDTEProject),
                buildProject => MicrosoftBuildEvaluationProjectUtility.RemoveImportStatement(buildProject, relativeTargetPath));

            // notify the project system of the change
            UpdateImportStamp(EnvDTEProject, true);
        }
    }
}
