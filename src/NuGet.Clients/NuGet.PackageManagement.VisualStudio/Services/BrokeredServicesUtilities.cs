// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System.Threading.Tasks;
using Microsoft;
using Microsoft.ServiceHub.Framework;
using NuGet.VisualStudio;

using IBrokeredServiceContainer = Microsoft.VisualStudio.Shell.ServiceBroker.IBrokeredServiceContainer;
using SVsBrokeredServiceContainer = Microsoft.VisualStudio.Shell.ServiceBroker.SVsBrokeredServiceContainer;

namespace NuGet.PackageManagement.VisualStudio
{
    public static class BrokeredServicesUtilities
    {
        public static async ValueTask<IServiceBroker> GetRemoteServiceBrokerAsync()
        {
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var serviceBrokerContainer = await ServiceLocator.GetGlobalServiceAsync<SVsBrokeredServiceContainer, IBrokeredServiceContainer>();
            Assumes.NotNull(serviceBrokerContainer);
            return serviceBrokerContainer.GetFullAccessServiceBroker();
        }
    }
}
