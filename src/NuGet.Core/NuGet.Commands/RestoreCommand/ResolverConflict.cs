﻿using System.Collections.Generic;
using System.Linq;
using NuGet.LibraryModel;

namespace NuGet.Commands
{
    public class ResolverConflict
    {
        public string Name { get; }
        public IList<ResolverRequest> Requests { get; }

        public ResolverConflict(string name, IEnumerable<ResolverRequest> requests)
        {
            Name = name;
            Requests = requests.ToList();
        }
    }
}