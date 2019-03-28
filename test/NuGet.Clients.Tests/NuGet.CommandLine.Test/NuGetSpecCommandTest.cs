using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class NuGetSpecCommandTests
    {
        [Theory]
        [InlineData("spec -AssemblyPath x b a")]
        [InlineData("spec a b -Force")]
        [InlineData("spec a b -?")]
        public void SpecCommand_Failure_InvalidArguments(string cmd)
        {
            Util.TestCommandInvalidArguments(cmd);
        }
    }
}
