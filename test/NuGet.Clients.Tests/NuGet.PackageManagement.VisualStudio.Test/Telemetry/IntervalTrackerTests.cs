// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using NuGet.VisualStudio;
using Xunit;

namespace NuGet.PackageManagement.VisualStudio.Test
{
    public class IntervalTrackerTests
    {

        [Fact]
        public void GetIntervals_WhenNoIntervalsAreCaptured_ReturnsEmpty()
        {
            // Setup
            var tracker = new IntervalTracker();

            // Act - do nothing

            // Assert
            Assert.Equal(0, tracker.GetIntervals().Count());
        }

        [Fact]
        public void GetIntervals_WhenNoEndIntervalMeasureIsCalled_ReturnsEmpty()
        {
            // Setup
            var tracker = new IntervalTracker();

            // Act - do nothing
            tracker.StartIntervalMeasure();
            tracker.StartIntervalMeasure();
            tracker.StartIntervalMeasure();
            // Assert
            Assert.Equal(0, tracker.GetIntervals().Count());
        }

        [Fact]
        public void GetIntervals_WhenStartIntervalIsNotCalled_IntervalsAreEmpty()
        {
            // Setup
            var tracker = new IntervalTracker();

            // Act - do nothing
            tracker.EndIntervalMeasure("one");
            tracker.EndIntervalMeasure("two");
            // Assert

            var allTimings = tracker.GetIntervals().ToList();

            Assert.Equal(2, allTimings.Count);
            Assert.True(allTimings.All(e => e.Item2.Equals(0)));
        }

        [Fact]
        public void IntervalTracker_AllIntervalsAreCaptured()
        {
            // Setup
            var tracker = new IntervalTracker();

            // Act
            tracker.StartIntervalMeasure();
            tracker.EndIntervalMeasure("first");

            tracker.StartIntervalMeasure();
            tracker.EndIntervalMeasure("second");

            tracker.StartIntervalMeasure();
            tracker.EndIntervalMeasure("third");

            // Assert

            var allTimings = tracker.GetIntervals().ToList();

            Assert.Equal(3, allTimings.Count);
            Assert.True(allTimings.Any(e => e.Item1.Equals("first")));
            Assert.True(allTimings.Any(e => e.Item1.Equals("second")));
            Assert.True(allTimings.Any(e => e.Item1.Equals("third")));
        }
    }
}
