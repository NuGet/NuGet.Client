// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using NuGet.PackageManagement.UI;
using NuGet.PackageManagement.VisualStudio;

namespace NuGet.SolutionRestoreManager
{
    internal sealed class WaitDialogProgress : RestoreOperationProgressUI
    {
        private readonly ThreadedWaitDialogHelper.Session _session;
        private readonly JoinableTaskFactory _taskFactory;

        private WaitDialogProgress(
            ThreadedWaitDialogHelper.Session session,
            JoinableTaskFactory taskFactory)
        {
            _session = session;
            _taskFactory = taskFactory;
            UserCancellationToken = _session.UserCancellationToken;
        }

        public static async Task<RestoreOperationProgressUI> StartAsync(
            IServiceProvider serviceProvider,
            JoinableTaskFactory jtf,
            bool isCancelable,
            CancellationToken token)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            if (jtf == null)
            {
                throw new ArgumentNullException(nameof(jtf));
            }

            return await jtf.RunAsync(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var waitDialogFactory = serviceProvider.GetService<
                    SVsThreadedWaitDialogFactory, IVsThreadedWaitDialogFactory>();

                var session = waitDialogFactory.StartWaitDialog(
                    waitCaption: Resources.DialogTitle,
                    initialProgress: new ThreadedWaitDialogProgressData(
                        Resources.RestoringPackages,
                        progressText: string.Empty,
                        statusBarText: string.Empty,
                        isCancelable: isCancelable,
                        currentStep: 0,
                        totalSteps: 0));

                return new WaitDialogProgress(session, jtf);
            });
        }

        public override void Dispose()
        {
            _taskFactory.Run(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                _session.Dispose();
            });
        }

        public override void ReportProgress(
            string progressMessage,
            uint currentStep,
            uint totalSteps)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // When both currentStep and totalSteps are 0, we get a marquee on the dialog
            var progressData = new ThreadedWaitDialogProgressData(
                progressMessage,
                progressText: string.Empty,
                statusBarText: string.Empty,
                isCancelable: true,
                currentStep: (int)currentStep,
                totalSteps: (int)totalSteps);

            _session.Progress.Report(progressData);
        }
    }
}
