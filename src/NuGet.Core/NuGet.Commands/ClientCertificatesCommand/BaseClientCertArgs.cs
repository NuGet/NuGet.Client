// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Commands
{
    public abstract class BaseClientCertArgs
    {
        /// <summary>
        ///     The NuGet configuration file. If specified, only the settings from this file will be used. If not specified, the
        ///     hierarchy of configuration files from the current directory will be used. To learn more about NuGet configuration
        ///     go to https://docs.microsoft.com/en-us/nuget/consume-packages/configuring-nuget-behavior.
        /// </summary>
        public string Configfile { get; set; }

        public virtual void Validate()
        {
        }
    }
}
