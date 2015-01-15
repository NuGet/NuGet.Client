using NuGet.ProjectManagement;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EnvDTEProject = EnvDTE.Project;

namespace NuGet.PackageManagement.VisualStudio
{
    public class FSharpProjectSystem: VSMSBuildNuGetProjectSystem
    {
        public FSharpProjectSystem(EnvDTEProject envDTEProject, INuGetProjectContext nuGetProjectContext)
            : base(envDTEProject, nuGetProjectContext)
        {
        }

        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "We want to swallow this exception. Read the comment below")]
        protected override void AddFileToProject(string path)
        {
            try
            {
                base.AddFileToProject(path);
            }
            catch
            {
                // HACK: The F# project system blows up with the following stack when calling AddFromFileCopy on a newly created folder:

                // at Microsoft.VisualStudio.FSharp.ProjectSystem.MSBuildUtilities.MoveFileToBottom(String relativeFileName, ProjectNode projectNode)
                // at Microsoft.VisualStudio.FSharp.ProjectSystem.FSharpProjectNode.MoveFileToBottomIfNoOtherPendingMove(String relativeFileName)
                // at Microsoft.VisualStudio.FSharp.ProjectSystem.ProjectNode.AddItem(UInt32 itemIdLoc, VSADDITEMOPERATION op, String itemName, UInt32 filesToOpen, String[] files, IntPtr dlgOwner, VSADDRESULT[] result)

                // But it still ends up working so we swallow the exception. We should follow up with F# people to find out what's going on.
            }
        }

        protected override void AddGacReference(string name)
        {
            // The F# project system expects assemblies that start with * to be framework assemblies.
            base.AddGacReference("*" + name);
        }

    }
}
