namespace NuGetClient.Test.Integration.Fixtures
{
    public enum TestRequirementErrorLevel
    {
        Fail,
        Warning
    }

    public class TestRequirementFile
    {
        public TestRequirementFile(string fileName, string relativePath)
            : this(fileName, relativePath, string.Empty)
        {
        }

        public TestRequirementFile(string fileName, string relativePath, string destPath)
        {
            this.FileName = fileName;
            this.RelativePath = relativePath;
            this.RelativeDestinationPath = destPath;
        }

        public TestRequirementErrorLevel ErrorLevel { get; set; } = TestRequirementErrorLevel.Fail;
        public string FileName { get; private set; }
        public string RelativePath { get; private set; }
        public string RelativeDestinationPath { get; private set; }
    }
}
