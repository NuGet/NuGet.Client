// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Common;

namespace NuGet.Commands
{
    public class PackCollectorLogger : LoggerBase, ICollectorLogger
    {
        public IEnumerable<IRestoreLogMessage> Errors => throw new System.NotImplementedException();

        public override void Log(ILogMessage message)
        {
            throw new System.NotImplementedException();
        }

        public override Task LogAsync(ILogMessage message)
        {
            throw new System.NotImplementedException();
        }
    }
}