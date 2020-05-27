// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.ServiceHub.Framework;
using NuGet.VisualStudio;

using IBrokeredServiceContainer = Microsoft.VisualStudio.Shell.ServiceBroker.IBrokeredServiceContainer;
using SVsBrokeredServiceContainer = Microsoft.VisualStudio.Shell.ServiceBroker.SVsBrokeredServiceContainer;

namespace NuGet.PackageManagement.VisualStudio
{
    public static class BrokeredServicesUtilities
    {
        public static async Task<IServiceBroker> GetRemoteServiceBroker()
        {
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var serviceBrokerContainer = await ServiceLocator.GetGlobalServiceAsync<SVsBrokeredServiceContainer, IBrokeredServiceContainer>();
            return serviceBrokerContainer.GetFullAccessServiceBroker();
        }
    }
}
