using System;
using System.IO;
using Moq;

namespace NuGet.CommandLine.Test
{
    public class PackageCreater
    {
        public static string CreatePackage(string id, string version, string outputDirectory,
            Action<PackageBuilder> additionalAction = null)
        {
            PackageBuilder builder = new PackageBuilder()
            {
                Id = id,
                Version = new SemanticVersion(version),
                Description = "Descriptions",
            };
            builder.Authors.Add("test");
            builder.Files.Add(CreatePackageFile(@"content\test1.txt"));
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
            PackageBuilder builder = new PackageBuilder()
            {
                Id = id,
                Version = new SemanticVersion(version),
                Description = "Descriptions",
            };
            builder.Authors.Add("test");
            builder.Files.Add(CreatePackageFile(@"content\symbol_test1.txt"));
            builder.Files.Add(CreatePackageFile(@"symbol.txt"));

            var packageFileName = Path.Combine(outputDirectory, id + "." + version + ".symbol.nupkg");
            using (var stream = new FileStream(packageFileName, FileMode.CreateNew))
            {
                builder.Save(stream);
            }

            return packageFileName;
        }

        private static IPackageFile CreatePackageFile(string name)
        {
            var file = new Mock<IPackageFile>();
            file.SetupGet(f => f.Path).Returns(name);
            file.Setup(f => f.GetStream()).Returns(new MemoryStream());

            string effectivePath;
            var fx = VersionUtility.ParseFrameworkNameFromFilePath(name, out effectivePath);
            file.SetupGet(f => f.EffectivePath).Returns(effectivePath);
            file.SetupGet(f => f.TargetFramework).Returns(fx);

            return file.Object;
        }
    }
}
