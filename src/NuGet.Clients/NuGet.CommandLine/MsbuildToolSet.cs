
namespace NuGet.CommandLine
{
    public class MsbuildToolSet
    {
        public MsbuildToolSet(string toolsVersion, string toolsPath)
        {
            ToolsPath = toolsPath;
            ToolsVersion = toolsVersion;
        }

        public string ToolsPath { get; }

        public string ToolsVersion { get; }
    }
}
