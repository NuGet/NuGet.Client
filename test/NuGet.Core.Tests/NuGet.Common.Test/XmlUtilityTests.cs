using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Common.Test
{
    public class XmlUtilityTests
    {
        [Fact]
        public void XmlUtility_Load_NullFilePathParameter_Throws()
        {
            //Act & Assert
            Assert.Throws(typeof(ArgumentException), () => XmlUtility.Load(null));
        }

        [Fact]
        public void XmlUtility_Load_SecureXml_Success()
        {
            using (var root = TestDirectory.Create())
            {
                //Arrange
                string path = Path.Combine(Path.GetDirectoryName(root), "packages.config");
                using (var writer = new StreamWriter(path))
                {
                    writer.Write(
@"<packages>
    <package id=""x"" version=""1.1.0"" targetFramework=""net45"" /> 
    <package id=""y"" version=""1.0.0"" targetFramework=""net45"" />
</packages>");
                }

                //Act
                XDocument doc = XmlUtility.Load(path);

                //Assert
                Assert.Equal("packages", doc.Root.Name);
                Assert.Equal(2, doc.Root.Elements().Count());
            }
        }

        [Fact]
        public void XmlUtility_Load_InSecureXml_Throws()
        {
            using (var root = TestDirectory.Create())
            {
                //Arrange
                string path = Path.Combine(Path.GetDirectoryName(root), "packages.config");
                using (var writer = new StreamWriter(path))
                {
                    writer.Write(
@"<!DOCTYPE package [
   <!ENTITY greeting ""Hello"">
   <!ENTITY name ""NuGet Client "">
   <!ENTITY sayhello ""&greeting; &name;"">
]>
<packages>
    <package id=""&sayhello;"" version=""1.1.0"" targetFramework=""net45"" /> 
    <package id=""x"" version=""1.0.0"" targetFramework=""net45"" />
</packages>");
                }

                //Act & Assert
                Assert.Throws(typeof(XmlException), () => XmlUtility.Load(path));
            }
        }
    }
}
