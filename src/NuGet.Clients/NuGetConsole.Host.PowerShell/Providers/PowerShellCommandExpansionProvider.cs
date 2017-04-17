// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;
using NuGet.VisualStudio;

namespace NuGetConsole.Host
{
    [SuppressMessage(
        "Microsoft.Performance",
        "CA1812:AvoidUninstantiatedInternalClasses",
        Justification = "MEF requires this class to be non-static.")]
    [Export(typeof(ICommandExpansionProvider))]
    [HostName(PowerShellHostProvider.HostName)]
    internal class PowerShellCommandExpansionProvider : CommandExpansionProvider
    {
        // Empty
    }

    /// <summary>
    /// Common ITabExpansion based command expansion provider implementation. This
    /// provider creates an ITabExpansion based CommandExpansion if a given host
    /// implements ITabExpansion.
    /// </summary>
    internal class CommandExpansionProvider : ICommandExpansionProvider
    {
        public ICommandExpansion Create(IHost host)
        {
            ITabExpansion tabExpansion = host as ITabExpansion;
            return tabExpansion != null ? CreateTabExpansion(tabExpansion) : null;
        }

        /// <summary>
        /// Create a ITabExpansion based command expansion instance. This base implementation
        /// creates a CommandExpansion instance.
        /// </summary>
        protected virtual ICommandExpansion CreateTabExpansion(ITabExpansion tabExpansion)
        {
            return new CommandExpansion(tabExpansion);
        }
    }
}
