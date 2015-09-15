// Guids.cs
// MUST match guids.h
using System;

namespace MicrosoftCorp.VSAPITest
{
    static class GuidList
    {
        public const string guidVSAPITestPkgString = "f1df8a2a-c9ef-4f9d-958b-e71422e4ab22";
        public const string guidVSAPITestCmdSetString = "4aed470e-c5d6-4d7a-adb8-afc3ae837f3c";

        public static readonly Guid guidVSAPITestCmdSet = new Guid(guidVSAPITestCmdSetString);
    };
}