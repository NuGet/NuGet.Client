// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Common
{
    /// <summary>
    /// static class to provide datetime common utility apis
    /// </summary>
    public class DatetimeUtility
    {
        /// <summary>
        /// take timespan n return in appropriate unit like ms, or seconds, or minutes, or hours
        /// </summary>
        /// <param name="time">timespan</param>
        /// <returns></returns>
        public static string ToReadableTimeFormat(TimeSpan time)
        {
            // initially define as hours
            double result = time.TotalHours;
            string type = "hr";

            if (time.TotalSeconds < 1)
            {
                result = time.TotalMilliseconds;
                type = "ms"; // milliseconds
            }
            else if (time.TotalMinutes < 1)
            {
                result = time.TotalSeconds;
                type = "sec"; // seconds
            }
            else if (time.TotalHours < 1)
            {
                result = time.TotalMinutes;
                type = "min"; // minutes
            }

            return string.Format("{0:0.##} {1}",result,type);
        }
    }
}
