// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.VisualStudio
{
    [Export(typeof(IOptionsPageActivator))]
    public class OptionsPageActivator : IOptionsPageActivator
    {
        // GUID of the Package Sources page, defined in PackageSourcesOptionsPage.cs
        private const string _packageSourcesGUID = "2819C3B6-FC75-4CD5-8C77-877903DE864C";

        // GUID of the General page, defined in GeneralOptionsPage.cs
        private const string _generalGUID = "0F052CF7-BF62-4743-B190-87FA4D49421E";

        private Action _closeCallback;
        private readonly AsyncLazy<IVsUIShell> _vsUIShell;

        [ImportingConstructor]
        public OptionsPageActivator()
        {
            _vsUIShell = new AsyncLazy<IVsUIShell>(async () =>
            {
                return await AsyncServiceProvider.GlobalProvider.GetServiceAsync<IVsUIShell>();
            },
            NuGetUIThreadHelper.JoinableTaskFactory);
        }

        public void NotifyOptionsDialogClosed()
        {
            if (_closeCallback != null)
            {
                // We want to clear the value of _closeCallback before invoking it.
                // Hence copying the value into a local variable.
                var callback = _closeCallback;
                _closeCallback = null;

                callback();
            }
        }

        public void ActivatePage(OptionsPage page, Action closeCallback)
        {
            _closeCallback = closeCallback;
            if (page == OptionsPage.General)
            {
                ShowOptionsPage(_generalGUID);
            }
            else if (page == OptionsPage.PackageSources)
            {
                ShowOptionsPage(_packageSourcesGUID);
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(page));
            }
        }

        private void ShowOptionsPage(string optionsPageGuid)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            object targetGuid = optionsPageGuid;
            var toolsGroupGuid = VSConstants.GUID_VSStandardCommandSet97;

            NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                IVsUIShell vsUIShell = await _vsUIShell.GetValueAsync();
                vsUIShell.PostExecCommand(
                    ref toolsGroupGuid,
                    (uint)VSConstants.cmdidToolsOptions,
                    (uint)0,
                    ref targetGuid);

            });
        }
    }
}
