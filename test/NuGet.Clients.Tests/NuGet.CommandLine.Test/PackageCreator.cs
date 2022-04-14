extern alias CoreV2;

using System;
using System.IO;
using Moq;

namespace NuGet.CommandLine.Test
{
    public class PackageCreater
    {
        public static string CreatePackage(string id, string version, string outputDirectory,
            Action<CoreV2.NuGet.PackageBuilder> additionalAction = null)
        {
            CoreV2.NuGet.PackageBuilder builder = new CoreV2.NuGet.PackageBuilder()
            {
                Id = id,
                Version = new CoreV2.NuGet.SemanticVersion(version),
                Description = "Descriptions",
            };
            builder.Authors.Add("test");
            builder.Files.Add(CreatePackageFile(Path.Combine("content", "test1.txt")));
            if (additionalAction != null)
            {
                additionalAction(builder);
            }

            var packageFileName = Path.Combine(outputDirectory, id + "." + version + ".nupkg");
            using (var stream = new FileStream(packageFileName, FileMode.CreateNew))
            {
                builder.Save(stream);
            }

            return packageFileName;
        }

        public static string CreateSymbolPackage(string id, string version, string outputDirectory)
        {
            CoreV2.NuGet.PackageBuilder builder = new CoreV2.NuGet.PackageBuilder()
            {
                Id = id,
                Version = new CoreV2.NuGet.SemanticVersion(version),
                Description = "Descriptions",
            };
            builder.Authors.Add("test");
            builder.Files.Add(CreatePackageFile(Path.Combine("content", "symbol_test1.txt")));
            builder.Files.Add(CreatePackageFile(@"symbol.txt"));

            var packageFileName = Path.Combine(outputDirectory, id + "." + version + ".symbol.nupkg");
            using (var stream = new FileStream(packageFileName, FileMode.CreateNew))
            {
                builder.Save(stream);
            }

            return packageFileName;
        }

        private static CoreV2.NuGet.IPackageFile CreatePackageFile(string name)
        {
            var file = new Mock<CoreV2.NuGet.IPackageFile>();
            file.SetupGet(f => f.Path).Returns(name);
            file.Setup(f => f.GetStream()).Returns(new MemoryStream());

            string effectivePath;
            var fx = CoreV2.NuGet.VersionUtility.ParseFrameworkNameFromFilePath(name, out effectivePath);
            file.SetupGet(f => f.EffectivePath).Returns(effectivePath);
            file.SetupGet(f => f.TargetFramework).Returns(fx);

            return file.Object;
        }
    }
}
