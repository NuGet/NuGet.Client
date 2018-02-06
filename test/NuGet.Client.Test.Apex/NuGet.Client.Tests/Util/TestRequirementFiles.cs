using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NuGetClient.Test.Foundation.Utility;
using NuGetClient.Test.Integration.Fixtures;

namespace NuGetClient.Test.Integration.Util
{
    public class TestRequirementFiles
    {
        internal static readonly string NTBinRoot = Environment.GetEnvironmentVariable("_NTBINROOT");
        public static readonly string ExePath = Assembly.GetExecutingAssembly().ManifestModule.FullyQualifiedName;

        public static string NuGetClientTestBinariesPath
        {
            get { return Path.Combine(TestRequirementFiles.InstallationPath, "NuGetClientTestBinariesPath"); }
        }

        public static string InstallationPath
        {
            get
            {
                return Path.Combine(Path.GetTempPath(), string.Format("{0}_{1}", VisualStudioInfo.Branch, "NuGetTest"));
            }
        }

        public static void CopyRequiredFiles(IEnumerable<TestRequirementFile> requiredTestFiles)
        {
            if (!Directory.Exists(TestRequirementFiles.NuGetClientTestBinariesPath))
            {
                Directory.CreateDirectory(TestRequirementFiles.NuGetClientTestBinariesPath);
            }

            foreach (TestRequirementFile testRequirementFile in requiredTestFiles)
            {
                TestRequirementFiles.CopyRequiredFile(testRequirementFile);
            }

            AppDomain.CurrentDomain.AssemblyResolve += delegate (object sender, ResolveEventArgs args)
            {
                return AssemblyResolver.ResolveAssembly(new string[] { TestRequirementFiles.InstallationPath }, args.Name);
            };
        }

        private static void CopyRequiredFile(TestRequirementFile file)
        {
            bool inRazzle = TestRequirementFiles.NTBinRoot == null ? false : true;

            string installFileLocation = Path.Combine(TestRequirementFiles.InstallationPath, file.RelativeDestinationPath, file.FileName);
            string localSuiteBinFilePath = Path.Combine(TestRequirementFiles.NTBinRoot ?? string.Empty, file.RelativePath, file.FileName);
            bool suiteBinFileExists = File.Exists(localSuiteBinFilePath);

            // If file already exists, check if there is a new version
            if (File.Exists(installFileLocation))
            {
                if (!inRazzle)
                {
                    return;
                }

                if (suiteBinFileExists)
                {
                    if (File.GetLastWriteTimeUtc(installFileLocation) >= File.GetLastWriteTimeUtc(localSuiteBinFilePath))
                    {
                        return;
                    }

                    // Installed file is stale, delete it so we can copy the newer version
                    TestRequirementFiles.ForceDelete(installFileLocation);
                }
            }

            if (inRazzle)
            {
                if (suiteBinFileExists)
                {
                    File.Copy(localSuiteBinFilePath, installFileLocation);
                }
                else
                {
                    TestRequirementFiles.LogOrThrowError(file.ErrorLevel, string.Format("Could not find file: {0}. Run 'task bliss integrationtests' to build all depedencies.", file.FileName));
                }
            }
            else
            {
                // We don't want to copy files down from the build share if we are in a razzle environment, the user should run "task bliss integrationtests"
                string cpvsbuildFilePath = Path.Combine(VisualStudioInfo.BuildShare, file.RelativePath, file.FileName);
                string currentBuildShareFileLocation = Path.Combine(VisualStudioInfo.CurrentBuildShare, file.RelativePath, file.FileName);
                if (File.Exists(cpvsbuildFilePath))
                {
                    File.Copy(cpvsbuildFilePath, installFileLocation);
                }
                else if (File.Exists(currentBuildShareFileLocation))
                {
                    File.Copy(currentBuildShareFileLocation, installFileLocation);
                }
                else
                {
                    TestRequirementFiles.LogOrThrowError(file.ErrorLevel, string.Format("Could not find file: {0} at {1} or {2} or {3}", file.FileName, InstallationPath, VisualStudioInfo.BuildShare, VisualStudioInfo.CurrentBuildShare));
                }
            }
        }

        private static void LogOrThrowError(TestRequirementErrorLevel errorLevel, string message)
        {
            if (errorLevel == TestRequirementErrorLevel.Fail)
            {
                throw new InvalidOperationException(message);
            }

            Console.WriteLine(message);
        }

        private static bool ForceDelete(string fileName)
        {
            if (Directory.Exists(fileName))
            {
                return Clean(fileName) != 0;
            }

            if (!File.Exists(fileName))
            {
                return true;
            }

            string renamedFile;
            int i = 0;
            for (i = 0; ; i++)
            {
                renamedFile = fileName + "." + i.ToString() + ".deleting";
                if (!File.Exists(renamedFile))
                {
                    break;
                }
            }

            File.Move(fileName, renamedFile);
            bool ret = TryDelete(renamedFile);
            if (i > 0)
            {
                // delete any old *.deleting files that may have been left around 
                string deletePattern = Path.GetFileName(fileName) + @".*.deleting";
                foreach (string deleteingFile in Directory.GetFiles(Path.GetDirectoryName(fileName), deletePattern))
                {
                    TryDelete(deleteingFile);
                }
            }
            return ret;
        }

        private static int Clean(string directory)
        {
            if (!Directory.Exists(directory))
            {
                return 0;
            }

            int ret = 0;
            foreach (string file in Directory.GetFiles(directory))
            {
                if (!ForceDelete(file))
                {
                    ret++;
                }
            }

            foreach (string subDir in Directory.GetDirectories(directory))
            {
                ret += Clean(subDir);
            }

            if (ret == 0)
            {
                try
                {
                    Directory.Delete(directory, true);
                }
                catch
                {
                    ret++;
                }
            }
            else
            {
                ret++;
            }

            return ret;
        }

        private static bool TryDelete(string fileName)
        {
            bool ret = false;
            try
            {
                FileAttributes attribs = File.GetAttributes(fileName);
                if ((attribs & FileAttributes.ReadOnly) != 0)
                {
                    attribs &= ~FileAttributes.ReadOnly;
                    File.SetAttributes(fileName, attribs);
                }
                File.Delete(fileName);
                ret = true;
            }
            catch (Exception)
            { }

            return ret;
        }
    }
}
