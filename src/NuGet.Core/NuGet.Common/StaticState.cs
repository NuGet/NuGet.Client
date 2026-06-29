// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Common
{
    /// <summary>
    /// Process-global hooks for refreshing or tearing down static and lingering process state around an
    /// MSBuild-driven restore. In a host that reuses its process across builds (MSBuild Server, multithreaded
    /// MSBuild) a task can no longer rely on process exit to discard state, so each cache or live resource
    /// subscribes a reset to one of these events - typically from its static constructor - and restore raises
    /// them, so the build behaves as if the process had started fresh. This is the only public surface for the
    /// feature; every contributing cache or resource stays internal to its own type.
    /// </summary>
    public static class StaticState
    {
        /// <summary>
        /// Raised at the start of an MSBuild-driven restore, before any restore work runs. Subscribers refresh
        /// state that may be stale in a reused process - chiefly caches derived from environment variables, which
        /// may have changed since a previous build.
        /// </summary>
        public static event Action? StartMSBuildRestoreTasks;

        /// <summary>
        /// Raised at the end of an MSBuild-driven restore. Subscribers tear down live OS resources (such as plugin
        /// processes) that the "process dies after each build" model relied on process exit to reclaim.
        /// </summary>
        public static event Action? EndMSBuildRestoreTasks;

        /// <summary>
        /// Raises <see cref="StartMSBuildRestoreTasks" />. Handlers are expected not to throw; one that can fail
        /// (for example, one that re-reads a value that could be malformed) is responsible for guarding itself, so
        /// the contract here stays honest and a genuine bug in a reset surfaces rather than being silently
        /// swallowed.
        /// </summary>
        public static void RaiseStartMSBuildRestoreTasks() => StartMSBuildRestoreTasks?.Invoke();

        /// <summary>
        /// Raises <see cref="EndMSBuildRestoreTasks" />. Handlers are expected not to throw; one that tears down an
        /// external resource is responsible for guarding itself, so a genuine bug in a reset surfaces rather than
        /// being silently swallowed.
        /// </summary>
        public static void RaiseEndMSBuildRestoreTasks() => EndMSBuildRestoreTasks?.Invoke();
    }
}
