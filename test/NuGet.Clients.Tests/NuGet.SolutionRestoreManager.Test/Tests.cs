using System;
using System.Collections.Concurrent;
using FluentAssertions;
using NuGet.VisualStudio;
using Xunit;

namespace NuGet.SolutionRestoreManager.Test
{
    public class Tests
    {
        [Fact]
        public void Tes2t()
        {
            var _pendingRequests = new Lazy<BlockingCollection<SolutionRestoreRequest>>(() => new BlockingCollection<SolutionRestoreRequest>(150));

            _pendingRequests.Value.TryAdd(SolutionRestoreRequest.OnUpdate());
            _pendingRequests.Value.Count.Should().Be(1);

            _pendingRequests.Value.TryAdd(SolutionRestoreRequest.OnUpdate());
            _pendingRequests.Value.Count.Should().Be(1);

            _pendingRequests.Value.TryTake(out var item);
            _pendingRequests.Value.Count.Should().Be(0);
        }
    }
}
