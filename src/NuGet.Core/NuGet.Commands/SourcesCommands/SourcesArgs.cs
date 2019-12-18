// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using NuGet.Common;
using NuGet.Configuration;

namespace NuGet.Commands
{
    public class SourcesArgs
    {
        public delegate void Log(string message);

        public ISettings Settings { get; set; }
        public IPackageSourceProvider SourceProvider { get; set; }
        public SourcesAction Action { get; set; }
        public string Name { get; set; }
        public string Source { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public bool StorePasswordInClearText { get; set; }
        public string ValidAuthenticationTypes { get; set; }
        public SourcesListFormat Format { get; set; }
        public bool IsQuiet { get; set; }
        public ILogger Logger { get; set; }
        public Log LogMinimalOverride { get; set; }
        public Log LogMinimal
        {
            get
            {
                return LogMinimalRespectingQuiet;
            }
        }

        public void LogMinimalRespectingQuiet(string data)
        {
            if (!IsQuiet)
            {
                if (LogMinimalOverride != null)
                {
                    LogMinimalOverride(data);
                }
                else
                {
                    Logger.LogMinimal(data);
                }
            }
        }
    }
}
