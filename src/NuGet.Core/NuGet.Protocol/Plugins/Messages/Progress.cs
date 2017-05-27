// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;

namespace NuGet.Protocol.Plugins
{
    /// <summary>
    /// A progress notification.
    /// </summary>
    public sealed class Progress
    {
        /// <summary>
        /// Gets the progress percentage.
        /// </summary>
        [JsonRequired]
        public double Percentage { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Progress" /> class.
        /// </summary>
        /// <param name="percentage">The progress percentage.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="percentage" />
        /// is not a valid percentage.</exception>
        [JsonConstructor]
        public Progress(double percentage)
        {
            if (!IsValidPercentage(percentage))
            {
                throw new ArgumentException(Strings.ArgumentCannotBeNullOrEmpty, nameof(percentage));
            }

            Percentage = percentage;
        }

        private static bool IsValidPercentage(double percentage)
        {
            if (double.IsNaN(percentage) || double.IsInfinity(percentage) ||
                percentage < 0 || percentage > 1)
            {
                return false;
            }

            return true;
        }
    }
}