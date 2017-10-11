using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Frameworks;
using Xunit;

namespace NuGet.ProjectModel.Test
{
    public class CacheFileFormatTests
    {
        [Theory]
        [InlineData("1", "LhkXQGGI+FQMy9dhLYjG5sWcHX3z/copzi4hjjBiY3Fotv0i7zQCikMZQ+rOKJ03gtx0hoHwIx5oKkM7sVHu7g==", "true", true)]
        [InlineData("2", "LhkXQGGI+FQMy9dhLYjG5sWcHX3z/copzi4hjjBiY3Fotv0i7zQCikMZQ+rOKJ03gtx0hoHwIx5oKkM7sVHu7g==", "true", false)]
        public void CacheFileFormat_CacheFileReadCorrectly(string v, string dgSpecHash, string success, bool expectedValid)
        {
            var cacheTemplate = @"{{
  ""version"": {0},
  ""dgSpecHash"": ""{1}"",
  ""success"": {2}
}}";
            CacheFile cacheFile = null;
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(string.Format(cacheTemplate, v, dgSpecHash, success))))
            {
                cacheFile = CacheFileFormat.Read(stream, NullLogger.Instance, "emptyPath");
            }
            Assert.Equal(expectedValid, cacheFile.IsValid);
            Assert.Equal(bool.Parse(success), cacheFile.Success);
            Assert.Equal(dgSpecHash, cacheFile.DgSpecHash);
            Assert.Equal(int.Parse(v), cacheFile.Version);
        }


        [Fact]
        public void CacheFileFormat_CacheFileWrittenCorrectly()
        {
            var v = "1";
            var dgSpecHash = "LhkXQGGI+FQMy9dhLYjG5sWcHX3z/copzi4hjjBiY3Fotv0i7zQCikMZQ+rOKJ03gtx0hoHwIx5oKkM7sVHu7g==";
            var success = "true";
            var cacheTemplate = @"{{
  ""version"": {0},
  ""dgSpecHash"": ""{1}"",
  ""success"": {2}
}}";

            var cacheFile = new CacheFile(dgSpecHash);
            cacheFile.Success = bool.Parse(success);
            using (var stream = new MemoryStream())
            {
                CacheFileFormat.Write(stream, cacheFile);
                var cacheString = Encoding.UTF8.GetString(stream.ToArray());

                Assert.Equal(string.Format(cacheTemplate, v, dgSpecHash, success), cacheString);
            }
        }
    }
}
