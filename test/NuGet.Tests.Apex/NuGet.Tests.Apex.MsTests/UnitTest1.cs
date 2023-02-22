// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Microsoft.Test.Apex;
using Microsoft.Test.Apex.VisualStudio;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace NuGet.Tests.Apex.MsTests
{
    [TestClass]
    public class UnitTest1 : ApexTest
    {
        [TestMethod]
        public void TestMethod1()
        {
            // Start an instance of VS
            var visualStudio = Operations.CreateHost<VisualStudioHost>();
            visualStudio.Start();

            visualStudio.Stop();
        }
    }
}
