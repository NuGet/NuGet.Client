// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NuGet.CommandLine.XPlat;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.XPlat.FuncTest
{
    [Collection("NuGet XPlat Test Collection")]
    public class XPlatVerifyTests
    {
        private static readonly string DotnetCli = DotnetCliUtil.GetDotnetCli();
        private static readonly string XplatDll = DotnetCliUtil.GetXplatDll();

        [Fact]
        public void Verify_MissingPackagePath_ThrowsAsync()
        {
            Assert.NotNull(DotnetCli);
            Assert.NotNull(XplatDll);

            string args = "verify";

            // Act
            var result = CommandRunner.Run(
              DotnetCli,
              Directory.GetCurrentDirectory(),
              $"{XplatDll} {args}",
              waitForExit: true);

            // Assert
            DotnetCliUtil.VerifyResultFailure(result, "Value cannot be null. (Parameter 'argument')");
        }
    }
}
