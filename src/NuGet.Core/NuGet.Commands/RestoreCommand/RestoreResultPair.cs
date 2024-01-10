// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NuGet.Commands
{
    public class RestoreResultPair
    {
        public RestoreSummaryRequest SummaryRequest { get; }

        public RestoreResult Result { get; }

        public RestoreResultPair(RestoreSummaryRequest request, RestoreResult result)
        {
            SummaryRequest = request;
            Result = result;
        }
    }
}
