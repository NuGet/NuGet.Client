// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.VisualStudio;
using Moq;

namespace Test.Utility.VisualStudio
{
    public static class NuGetExperimentationServiceUtility
    {
        public static Mock<INuGetExperimentationService> GetMock()
        {
            var mock = new Mock<INuGetExperimentationService>();
            return mock;
        }
    }
}
