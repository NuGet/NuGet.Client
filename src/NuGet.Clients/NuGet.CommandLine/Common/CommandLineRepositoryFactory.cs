// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Protocol.Core.Types;

namespace NuGet.Common
{
    public class CommandLineRepositoryFactory : PackageRepositoryFactory
    {
        private readonly IConsole _console;

        public CommandLineRepositoryFactory(IConsole console)
        {
            _console = console;
        }

        public override IPackageRepository CreateRepository(string packageSource)
        {
            var repository = base.CreateRepository(packageSource);
            var httpClientEvents = repository as IHttpClientEvents;
            if (httpClientEvents != null)
            {
                httpClientEvents.SendingRequest += (sender, args) =>
                {
                    if (sender != httpClientEvents)
                    {
                        return;
                    }

                    if (_console.Verbosity == Verbosity.Detailed)
                    {
                        _console.WriteLine(
                            System.ConsoleColor.Green,
                            "{0} {1}", args.Request.Method, args.Request.RequestUri);
                    }

                    var userAgentString = new UserAgentStringBuilder(CommandLineConstants.UserAgent).Build();
                    HttpUtility.SetUserAgent(args.Request, userAgentString);
                };
            }

            return repository;
        }
    }
}