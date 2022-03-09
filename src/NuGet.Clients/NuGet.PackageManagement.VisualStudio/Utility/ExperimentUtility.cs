// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.VisualStudio.Shell;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.VisualStudio
{
    internal class ExperimentUtility
    {
        internal static readonly Microsoft.VisualStudio.Threading.AsyncLazy<bool> IsTransitiveOriginExpEnabled = new(async () =>
        {
            bool isExpEnabled;
            try
            {
                var svc = await ServiceLocator.GetComponentModelServiceAsync<INuGetExperimentationService>();
                isExpEnabled = svc?.IsExperimentEnabled(ExperimentationConstants.TransitiveDependenciesInPMUI) ?? false;
            }
            catch (ServiceUnavailableException)
            {
                isExpEnabled = false;
            }

            return isExpEnabled ? isExpEnabled : true;

        }, NuGetUIThreadHelper.JoinableTaskFactory);
    }
}
