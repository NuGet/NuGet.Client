// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;

namespace NuGet.Commands
{
    public interface ISignCommandRunner
    {
        int ExecuteCommand(SignArgs signArgs);
    }
}
