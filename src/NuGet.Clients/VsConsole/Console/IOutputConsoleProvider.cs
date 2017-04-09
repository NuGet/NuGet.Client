// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetConsole
{
    public interface IOutputConsoleProvider
    {
        /// <summary>
        /// Creates a console enabling writing to the VS build output window pane.
        /// </summary>
        /// <returns>Output console instance.</returns>
        IOutputConsole CreateBuildOutputConsole();

        /// <summary>
        /// Creates a console instance associated with the Package Manager output window.
        /// </summary>
        /// <returns>Output console instance.</returns>
        IOutputConsole CreatePackageManagerConsole();

        /// <summary>
        /// Creates host-enabled console instance.
        /// </summary>
        /// <returns>Console instance.</returns>
        IConsole CreatePowerShellConsole();

        /// <summary>
        /// Back-compat API for creating a PM output console.
        /// </summary>
        /// <param name="requirePowerShellHost">Creates a console with host attached if true.</param>
        /// <returns>Console instance.</returns>
        [System.Obsolete("Method is deprecated")]
        IConsole CreateOutputConsole(bool requirePowerShellHost);
    }
}
