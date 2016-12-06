﻿using System;
using Xunit;
using NuGet.Common;

namespace NuGet.CommandLine.Test
{
    /// <summary>
    /// This attribute ensures the Fact is only run on Windows.
    /// </summary>
    public class WindowsNTFactAttribute
        : FactAttribute
    {
        public WindowsNTFactAttribute()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                Skip = "Test only runs on Windows NT or later.";
            }
        }
    }

    public class UnixMonoFactAttribute
        : FactAttribute
    {
        public UnixMonoFactAttribute()
        {
            if (!RuntimeEnvironmentHelper.IsMono || RuntimeEnvironmentHelper.IsWindows)
            {
                Skip = "Test only runs with mono on Unix.";
            }
        }
    }
}
