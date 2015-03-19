// PkgCmdID.cs
// MUST match PkgCmdID.h
using System;

namespace MicrosoftCorp.VSAPITest
{
    static class PkgCmdIDList
    {
        public const uint cmdidNuGetAPITest = 0x100;
        public const uint cmdidNuGetAPIInstallPackage = 0x200;
        public const uint cmdidNuGetAPIInstallBadSource = 0x300;

        public const uint cmdidNuGetAPIInstallPackageAsync = 0x400;
        public const uint cmdidNuGetAPIGetSources = 0x500;
        public const uint cmdidNuGetAPIGetOfficialSources = 0x600;

        public const uint cmdidNuGetAPIInstallPackageEmptyVersion = 0x601;

        public const uint cmdidNuGetAPIUninstallPackage = 0x602;
        public const uint cmdidNuGetAPIUninstallPackageNoDep = 0x603;
        public const uint cmdidNuGetAPIUninstallPackageNoForce = 0x604;

        public const uint cmdidNuGetAPICheck = 0x9900;
    };
}