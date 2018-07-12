// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Management.Automation;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace API.Test.Cmdlets
{
    [Cmdlet(VerbsDiagnostic.Test, "Project")]
    [OutputType(typeof(bool))]
    public sealed class TestProjectCommand : TestExtensionCmdlet
    {
        private bool _isDeferred;

        [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        public string UniqueName { get; set; }

        [Parameter]
        public SwitchParameter IsDeferred { get => _isDeferred; set => _isDeferred = value; }

        protected override async Task ProcessRecordAsync()
        {
            var project = await VSSolutionHelper.FindProjectByUniqueNameAsync(UniqueName);
            if (project == null)
            {
                throw new ItemNotFoundException($"Project '{UniqueName}' is not found.");
            }

            if (IsDeferred)
            {
                WriteObject(await TestProjectIsDeferredAsync(project));
                return;
            }

            WriteObject(true);
        }

        private static async Task<bool> TestProjectIsDeferredAsync(IVsHierarchy project)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            object isDeferred;
            if (ErrorHandler.Failed(project.GetProperty(
                (uint)VSConstants.VSITEMID.Root,
                (int)__VSHPROPID9.VSHPROPID_IsDeferred,
                out isDeferred)))
            {
                return false;
            }

            return object.Equals(true, isDeferred);
        }
    }
}
