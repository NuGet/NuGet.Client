// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.VisualStudio;
using NuGetConsole.Implementation;

namespace NuGetConsole
{
    public static class CommandUiUtilities
    {
        private static readonly Lazy<IVsInvalidateCachedCommandState> CommandStateCacheInvalidator;

        static CommandUiUtilities()
        {
            CommandStateCacheInvalidator = new Lazy<IVsInvalidateCachedCommandState>(
                () => ServiceLocator.GetGlobalServiceFreeThreaded<SVsInvalidateCachedCommandState, IVsInvalidateCachedCommandState>());
        }

        public static void InvalidateDefaultProject()
        {
            var command = new VSCommandId()
            {
                CommandSet = GuidList.guidNuGetCmdSet,
                CommandId = PkgCmdIDList.cmdidProjects
            };

            CommandStateCacheInvalidator.Value.InvalidateSpecificCommandUIState(command);
        }
    }
}