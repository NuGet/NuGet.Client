using System;
using System.Runtime.CompilerServices;
using NuGet.Common;
using Xunit;

namespace NuGet.CommandLine.Test
{
    /// <summary>
    /// This attribute ensures the Fact is only run on Windows.
    /// </summary>
    public class WindowsNTFactAttribute
        : FactAttribute
    {
        public WindowsNTFactAttribute(
            [CallerFilePath] string? sourceFilePath = null,
            [CallerLineNumber] int sourceLineNumber = 0)
            : base(sourceFilePath, sourceLineNumber)
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
        public UnixMonoFactAttribute(
            [CallerFilePath] string? sourceFilePath = null,
            [CallerLineNumber] int sourceLineNumber = 0)
            : base(sourceFilePath, sourceLineNumber)
        {
            if (!RuntimeEnvironmentHelper.IsMono || RuntimeEnvironmentHelper.IsWindows)
            {
                Skip = "Test only runs with mono on Unix.";
            }
        }
    }

    public class SkipMonoAttribute
        : FactAttribute
    {
        public SkipMonoAttribute(
            [CallerFilePath] string? sourceFilePath = null,
            [CallerLineNumber] int sourceLineNumber = 0)
            : base(sourceFilePath, sourceLineNumber)
        {
            if (RuntimeEnvironmentHelper.IsMono)
            {
                Skip = "Skip this test on mono for now.";
            }
        }
    }

    public class SkipMonoTheoryAttribute
       : TheoryAttribute
    {
        public SkipMonoTheoryAttribute(
            [CallerFilePath] string? sourceFilePath = null,
            [CallerLineNumber] int sourceLineNumber = 0)
            : base(sourceFilePath, sourceLineNumber)
        {
            if (RuntimeEnvironmentHelper.IsMono)
            {
                Skip = "Skip this test on mono for now.";
            }
        }
    }
}
