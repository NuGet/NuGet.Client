using System;
using System.Collections.Generic;
using NuGet.LibraryModel;

namespace NuGet.DependencyResolver
{
    public class RemoteMatch
    {
        public IRemoteDependencyProvider Provider { get; set; }
        public Library Library { get; set; }
        public string Path { get; set; }
    }
}