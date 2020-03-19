// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace NuGet.VisualStudio
{
    public class IntervalTracker
    {
        private readonly Stopwatch _intervalWatch = new Stopwatch();
        private readonly List<Tuple<string, TimeSpan>> _intervalList = new List<Tuple<string, TimeSpan>>();

        public void StartIntervalMeasure()
        {
            _intervalWatch.Restart();
        }

        public void EndIntervalMeasure(string propertyName)
        {
            _intervalWatch.Stop();
            _intervalList.Add(new Tuple<string, TimeSpan>(propertyName, _intervalWatch.Elapsed));
        }

        public IEnumerable<(string, double)> GetIntervals()
        {
            foreach (var interval in _intervalList)
            {
                yield return (interval.Item1, interval.Item2.TotalSeconds);
            }
        }
    }
}
