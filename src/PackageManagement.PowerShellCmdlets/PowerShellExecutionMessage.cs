// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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
        public LogMessage(ProjectManagement.MessageLevel level, string content)
        {
            Level = level;
            Content = content;
        }

        public ProjectManagement.MessageLevel Level { get; set; }

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
