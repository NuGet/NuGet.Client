// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.Commands
{
    /// <summary>
    /// Command Runner used to run the business logic for nuget locals command
    /// </summary>
    public class ListCommandRunner : IListCommandRunner
    {
        /// <summary>
        /// Executes the logic for nuget locals command.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<IPackageSearchMetadata> ExecuteCommand(ListArgs listArgs)
        {
            return null;
        }
    }
}