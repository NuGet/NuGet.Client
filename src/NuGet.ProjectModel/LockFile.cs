using System;
using System.Collections.Generic;

namespace NuGet.ProjectModel
{
    public class LockFile
    {
        public bool Islocked { get; set; }
        public IList<LockFileLibrary> Libraries { get; set; } = new List<LockFileLibrary>();
    }
}