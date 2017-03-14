// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;
using System.Security;
using System.Threading.Tasks;
using Microsoft.VisualStudio.ComponentModelHost;
using VsPackage = Microsoft.VisualStudio.Shell.Package;

namespace NuGetConsole
{
    [Export(typeof(IConsoleInitializer))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class ConsoleInitializer : IConsoleInitializer
    {
        [SuppressMessage("Microsoft.VisualStudio.Threading.Analyzers", "VSTHRD011", Justification = "NuGet/Home#4833 Baseline")]
        private readonly Lazy<Task<Action>> _initializeTask = new Lazy<Task<Action>>(GetInitializeTask);

        public Task<Action> Initialize()
        {
            return _initializeTask.Value;
        }

        private static Task<Action> GetInitializeTask()
        {
            var componentModel = (IComponentModel)VsPackage.GetGlobalService(typeof(SComponentModel));
            if (componentModel == null)
            {
                throw new InvalidOperationException();
            }

            try
            {
                // HACK: Short cut to set the Powershell execution policy for this process to RemoteSigned.
                // This is so that we can initialize the PowerShell host and load our modules successfully.
                Environment.SetEnvironmentVariable(
                    "PSExecutionPolicyPreference", "RemoteSigned", EnvironmentVariableTarget.Process);
            }
            catch (SecurityException)
            {
                // ignore if user doesn't have permission to add process-level environment variable,
                // which is very rare.
            }

            var initializer = componentModel.GetService<IHostInitializer>();
            return Task.Factory.StartNew(state =>
                {
                    var hostInitializer = (IHostInitializer)state;
                    if (hostInitializer != null)
                    {
                        hostInitializer.Start();
                        return (Action)hostInitializer.SetDefaultRunspace;
                    }
                    return delegate { };
                },
                initializer);
        }
    }
}
