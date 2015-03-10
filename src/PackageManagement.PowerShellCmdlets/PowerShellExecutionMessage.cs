using NuGet.ProjectManagement;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    public class Message 
    {
    }

    public class ExecutionCompleteMessage : Message
    {
    }

    public class LogMessage : Message
    {
        public LogMessage(MessageLevel level, string content)
        {
            Level = level;
            Content = content;
        }

        public MessageLevel Level { get; set; }

        public string Content { get; set; }
    }

    public class ScriptMessage : Message
    {
        public ScriptMessage(string scriptPath)
        {
            ScriptPath = scriptPath;
        }

        public string ScriptPath { get; set; }
    }
}
