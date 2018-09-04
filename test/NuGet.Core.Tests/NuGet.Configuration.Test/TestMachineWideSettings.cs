// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Configuration.Test
{
    public class TestMachineWideSettings : IMachineWideSettings
    {
        public Settings Settings { get; }

        public TestMachineWideSettings(Settings settings)
        {
            Settings = settings;
        }
    }
}
