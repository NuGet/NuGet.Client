using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.Protocol.Core.v3.Tests
{
    public class TestLocalPackageInfo : LocalPackageInfo
    {
        public TestLocalPackageInfo(string id, string version)
            : base()
        {
            IdentityValue = new PackageIdentity(id, NuGetVersion.Parse(version));
        }

        public string PathValue { get; set; }
        public PackageIdentity IdentityValue { get; set; }
        public PackageReaderBase PackageValue { get; set; }
        public NuspecReader NuspecValue { get; set; }

        public override string Path
        {
            get
            {
                return PathValue;
            }
        }

        public override PackageIdentity Identity
        {
            get
            {
                return IdentityValue;
            }
        }

        public override PackageReaderBase GetReader()
        {
            return PackageValue;
        }

        public override NuspecReader Nuspec
        {
            get
            {
                return NuspecValue;
            }
        }
    }
}