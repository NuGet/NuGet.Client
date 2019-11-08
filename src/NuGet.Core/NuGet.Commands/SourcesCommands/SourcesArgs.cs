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

        public ISettings Settings { get; }
        public IPackageSourceProvider SourceProvider { get; }
        public SourcesAction Action { get; }
        public string Name { get; }
        public string Source { get; }
        public string Username { get; }
        public string Password { get; }
        public bool StorePasswordInClearText { get; }
        public string ValidAuthenticationTypes { get; }
        public SourcesListFormat Format { get; }
        public bool Interactive { get; }
        public string ConfigFile { get; }
        private bool IsQuiet { get; }
        public Log LogError { get; }
        public Log LogMinimal { get; }
        public Log LogQuiet { get; }

        public void LogQuietImplementation(string data)
        {
            if (IsQuiet)
            {
                LogMinimal(data);
            }
        }
        
        public SourcesArgs(
            ISettings settings,
            IPackageSourceProvider sourceProvider,
            SourcesAction action,
            string name,
            string source,
            string username,
            string password,
            bool storePasswordInClearText,
            string validAuthenticationTypes,
            SourcesListFormat format,
            bool interactive,
            string configFile,
            bool isQuiet,
            Log logError,
            Log logMinimal
            )
        {
            Settings = settings;
            SourceProvider = sourceProvider;
            Action = action;
            Name = name;
            Source = source;
            Username = username;
            Password = password;
            StorePasswordInClearText = storePasswordInClearText;
            ValidAuthenticationTypes = validAuthenticationTypes;
            Format = format;
            Interactive = interactive;
            ConfigFile = configFile;
            IsQuiet = isQuiet;
            LogError = logError;
            LogMinimal = logMinimal;
            LogQuiet = LogQuietImplementation;
        }
    }
}
