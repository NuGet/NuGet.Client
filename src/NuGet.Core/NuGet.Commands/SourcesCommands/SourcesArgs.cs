// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using NuGet.Configuration;

namespace NuGet.Commands
{
    public class SourcesArgs
    {
        public delegate void Log(string message);

        public ISettings Settings { get; }
        public IPackageSourceProvider SourceProvider { get; }
        public string Action { get; }
        public string Name { get; }
        public string Source { get; }
        public string Username { get; }
        public string Password { get; }
        public bool StorePasswordInClearText { get; }
        public string ValidAuthenticationTypes { get; }
        public string Format { get; }
        public bool Interactive { get; }
        public string ConfigFile { get; }
        public string CurrentDirectory { get; }
        public Log LogError { get; }
        public Log LogInformation { get; }

        public SourcesArgs(
            ISettings settings,
            IPackageSourceProvider sourceProvider,
            string action,
            string name,
            string source,
            string username,
            string password,
            bool storePasswordInClearText,
            string validAuthenticationTypes,
            string format,
            bool interactive,
            string configFile,
            Log logError,
            Log logInformation
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
            LogError = logError;
            LogInformation = logInformation;
        }
    }
}
