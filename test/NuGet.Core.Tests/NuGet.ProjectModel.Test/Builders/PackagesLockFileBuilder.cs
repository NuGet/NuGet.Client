using System;
using System.Collections.Generic;

namespace NuGet.ProjectModel.Test.Builders
{
    internal class PackagesLockFileBuilder
    {
        private int _version = PackagesLockFileFormat.Version;
        private List<PackagesLockFileTarget> _targets = new List<PackagesLockFileTarget>();

        public PackagesLockFileBuilder WithVersion(int version)
        {
            _version = version;
            return this;
        }

        public PackagesLockFileBuilder WithTarget(Action<PackagesLockFileTargetBuilder> action)
        {
            var target = new PackagesLockFileTargetBuilder();
            action(target);
            _targets.Add(target.Build());
            return this;
        }

        public PackagesLockFile Build()
        {
            return new PackagesLockFile
            {
                Version = _version,
                Targets = _targets
            };
        }
    }
}
