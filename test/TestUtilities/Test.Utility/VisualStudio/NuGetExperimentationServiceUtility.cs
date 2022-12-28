// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System.Collections.Generic;
using Microsoft.VisualStudio.Experimentation;
using Moq;

namespace Test.Utility.VisualStudio
{
    public static class NuGetExperimentationServiceUtility
    {
        public static IExperimentationService GetMock(Dictionary<string, bool>? flights = null)
        {
            var mock = new Mock<IExperimentationService>();
            var d = mock.Setup(m => m.IsCachedFlightEnabled(It.IsAny<string>()));

            if (flights is null)
            {
                d.Returns(false);
            }
            else
            {
                d.Returns((string s) => flights.ContainsKey(s) && flights[s]);
            }

            return mock.Object;
        }
    }
}
