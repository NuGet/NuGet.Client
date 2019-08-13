using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace NuGet.Test.Utility
{
    public class TestDirectoryBuilder
    {
        public NuspecBuilder NuspecBuilder { get; private set; }
        public string NuspecFile { get; private set; }
        public Dictionary<string, int> Files { get; private set; }
        public Dictionary<string, string> TextFiles { get; private set; }
        public string BaseDir { get; private set; }
        public string NuspecPath => Path.Combine(BaseDir, NuspecFile);

        private TestDirectoryBuilder()
        {
            Files = new Dictionary<string, int>();
            TextFiles = new Dictionary<string, string>();
        }

        public static TestDirectoryBuilder Create()
        {
            return new TestDirectoryBuilder();
        }

        public TestDirectoryBuilder WithNuspec(NuspecBuilder nuspecBuilder, string filepath = "Package.nuspec")
        {
            NuspecBuilder = nuspecBuilder;
            NuspecFile = filepath;
            return this;
        }

        public TestDirectoryBuilder WithFile(string filepath, int size)
        {
            Files.Add(filepath, size);
            return this;
        }

        public TestDirectoryBuilder WithTextFile(string filepath, string content)
        {
            TextFiles.Add(filepath, content);
            return this;
        }

        public TestDirectory Build()
        {
            TestDirectory testDirectory = TestDirectory.Create();
            BaseDir = testDirectory.Path;

            CreateFiles();
            CreateTextFiles();

            if (NuspecBuilder != null)
            {
                File.WriteAllText(NuspecPath, NuspecBuilder.Build().ToString());
            }

            return testDirectory;
        }

        private void CreateFiles()
        {
            foreach (var f in Files)
            {
                var filepath = Path.Combine(BaseDir, f.Key);
                var dir = Path.GetDirectoryName(filepath);

                Directory.CreateDirectory(dir);
                using (var fileStream = File.OpenWrite(Path.Combine(BaseDir, f.Key)))
                {
                    fileStream.SetLength(f.Value);
                }
            }
        }

        private void CreateTextFiles()
        {
            foreach (var f in TextFiles)
            {
                var filepath = Path.Combine(BaseDir, f.Key);
                var dir = Path.GetDirectoryName(filepath);

                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(BaseDir, f.Key), f.Value);
            }
        }
    }
}
