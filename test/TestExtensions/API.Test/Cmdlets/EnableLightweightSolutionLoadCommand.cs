// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Management.Automation;
using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace API.Test.Cmdlets
{
    [Cmdlet(VerbsLifecycle.Enable, "LightweightSolutionLoad")]
    public sealed class EnableLightweightSolutionLoadCommand : TestExtensionCmdlet
    {
        private bool _reload;

        [Parameter]
        public SwitchParameter Reload { get => _reload; set => _reload = value; }

        protected override async Task ProcessRecordAsync()
        {
            await EnableDeferredLoadAsync();

            if (_reload)
            {
                await ReloadSolutionAsync();

                VSSolutionHelper.WaitForSolutionLoad();

                await Task.Delay(TimeSpan.FromSeconds(3));
            }
        }

        private static async Task EnableDeferredLoadAsync()
        {
#if VS14
            await Task.Yield();
            throw new System.NotSupportedException("Operation not supported for VS14");
#else
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var vsSolution = ServiceLocator.GetService<SVsSolution, IVsSolution>();

            ErrorHandler.ThrowOnFailure(vsSolution.SetProperty(
                (int)__VSPROPID7.VSPROPID_DeferredLoadOption,
                __VSSOLUTIONDEFERREDLOADOPTION.DLO_DEFERRED));
#endif
        }

        private static async Task ReloadSolutionAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var dteSolution = await VSSolutionHelper.GetDTESolutionAsync();
            Assumes.Present(dteSolution);

            var solutionFullName = dteSolution.FullName;

            dteSolution.Close(SaveFirst: true);

            dteSolution.Open(solutionFullName);
        }
    }
}
