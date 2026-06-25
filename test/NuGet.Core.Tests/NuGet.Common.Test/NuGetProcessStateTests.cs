// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Xunit;

namespace NuGet.Common.Test
{
    public class NuGetProcessStateTests
    {
        // NuGetProcessState is a process-global registry with no unregister, and product types register their own
        // resets, so these tests use a private marker action and assert only on its own local counter. Other actions
        // registered (by products or earlier tests) may also run during Reset but cannot affect that counter.

        [Fact]
        public void RegisterResetAction_NullAction_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(
                () => NuGetProcessState.RegisterResetAction(NuGetProcessState.ResetKey.StartRestore, null!));
        }

        [Fact]
        public void Reset_RunsActionRegisteredForKey()
        {
            int ran = 0;
            NuGetProcessState.RegisterResetAction(NuGetProcessState.ResetKey.StartRestore, () => ran++);

            NuGetProcessState.Reset(NuGetProcessState.ResetKey.StartRestore);

            Assert.Equal(1, ran);
        }

        [Fact]
        public void Reset_RunsEveryActionRegisteredForKey()
        {
            int firstRan = 0;
            int secondRan = 0;
            NuGetProcessState.RegisterResetAction(NuGetProcessState.ResetKey.StartRestore, () => firstRan++);
            NuGetProcessState.RegisterResetAction(NuGetProcessState.ResetKey.StartRestore, () => secondRan++);

            NuGetProcessState.Reset(NuGetProcessState.ResetKey.StartRestore);

            Assert.Equal(1, firstRan);
            Assert.Equal(1, secondRan);
        }

        [Fact]
        public void Reset_DoesNotRunActionRegisteredForADifferentKey()
        {
            int ran = 0;
            NuGetProcessState.RegisterResetAction(NuGetProcessState.ResetKey.StartRestore, () => ran++);

            // Resetting a different key must not run the StartRestore action.
            NuGetProcessState.Reset(NuGetProcessState.ResetKey.EndRestore);
            Assert.Equal(0, ran);

            // Resetting its own key runs it.
            NuGetProcessState.Reset(NuGetProcessState.ResetKey.StartRestore);
            Assert.Equal(1, ran);
        }
    }
}
