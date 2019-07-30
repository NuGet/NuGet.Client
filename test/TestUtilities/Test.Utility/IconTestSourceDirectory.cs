using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NuGet.Test.Utility
{
    /// <summary>
    /// Package directory for testing embedded icon functionality with nuspec files
    /// </summary>
    public sealed class IconTestSourceDirectory : IDisposable
    {
        private const string NuspecFilename = "iconPackage.nuspec";
        public string IconEntry { get; set; }

        /// <summary>
        /// Base directory for test
        /// </summary>
        private TestDirectory TestDirectory { get; set; }

        public string BaseDir => TestDirectory.Path;

        public string NuspecPath => Path.Combine(BaseDir, NuspecFilename);

        /// <summary>
        /// Constructor for test cases with one icon file.
        /// </summary>
        /// <param name="iconName">&lt;icon /> entry in the nuspec.</param>
        /// <param name="fileName">Package relative icon file name.</param>
        /// <param name="iconFileSize">Icon file size. If it is less than zero, it will not write the file.</param>
        public IconTestSourceDirectory(string iconName, string fileName, int iconFileSize)
        {
            IconEntry = iconName;

            var entriesList = new List<Tuple<string, string>>();

            var fileList = new List<Tuple<string, int>>();

            if (!string.IsNullOrEmpty(fileName))
            {
                entriesList.Add(Tuple.Create(fileName, string.Empty));
            }

            if (iconFileSize > 0)
            {
                fileList.Add(Tuple.Create(fileName, iconFileSize));
            }

            TestDirectory = TestDirectory.Create();

            CreateFiles(fileList);
            CreateNuspec(entriesList);
        }


        /// <summary>
        /// Constructor for test cases with multiple files and multiple &lt;file /&gt; entries in the nuspec.
        /// </summary>
        /// <param name="iconName">&lt;icon /> entry in the nuspec.</param>
        /// <param name="files">List of (filePath, fileSize) tuples for the test files to create</param>
        /// <param name="fileEntries">List of strings written as &lt;file src="{entry}" /&gt; </param>
        public IconTestSourceDirectory(string iconName, IEnumerable<Tuple<string, int>> files, IEnumerable<string> fileEntries)
        {
            IconEntry = iconName;

            TestDirectory = TestDirectory.Create();

            CreateFiles(files);
            CreateNuspec(fileEntries.Select(x => Tuple.Create(x, string.Empty)));
        }

        /// <summary>
        /// Constructor for test cases with multiple files and multiple &lt;file /&gt; entries with target attribute.
        /// </summary>
        /// <param name="iconName">&lt;icon /> entry in the nuspec.</param>
        /// <param name="files">List of (filePath, fileSize) tuples for the test files to create</param>
        /// <param name="fileEntries">List of (src, tgt) tuples written as &lt;file src="{src}" target="{tgt}" /&gt; </param>
        public IconTestSourceDirectory(string iconName, IEnumerable<Tuple<string, int>> files, IEnumerable<Tuple<string, string>> fileEntries)
        {
            IconEntry = iconName;

            TestDirectory = TestDirectory.Create();

            CreateFiles(files);
            CreateNuspec(fileEntries);
        }

        private void CreateFiles(IEnumerable<Tuple<string, int>> files)
        {
            foreach (var f in files)
            {
                var filepath = Path.Combine(BaseDir, f.Item1);
                var dir = Path.GetDirectoryName(filepath);

                Directory.CreateDirectory(dir);
                using (var fileStream = File.OpenWrite(Path.Combine(BaseDir, f.Item1)))
                {
                    fileStream.SetLength(f.Item2);
                }
            }
        }

        private void CreateNuspec(IEnumerable<Tuple<string, string>> fileEntries)
        {
            StringBuilder sb = new StringBuilder();

            sb.Append(@"<?xml version=""1.0"" encoding=""utf-8""?>
                            <package>
                              <metadata>
                                <id>iconPackage</id>
                                <version>5.2.0</version>
                                <authors>Author1, author2</authors>
                                <description>Sample icon description</description>");

            if (!string.IsNullOrEmpty(IconEntry))
            {
                sb.Append("<icon>");
                sb.Append(IconEntry);
                sb.Append("</icon>\n");
            }

            sb.Append(@"</metadata>
                          <files>");
            foreach (var fe in fileEntries)
            {
                sb.Append($"<file src=\"{fe.Item1}\"");
                if (!string.Empty.Equals(fe.Item2))
                {
                    sb.Append($" target=\"{fe.Item1}\"");
                }
                sb.Append(" />\n");
            }
            sb.Append(@"</files>
                            </package>");

            File.WriteAllText(NuspecPath, sb.ToString());
        }

        public void Dispose()
        {
            TestDirectory.Dispose();
        }
    }
}
