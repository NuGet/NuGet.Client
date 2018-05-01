// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Build.Framework;

namespace Microsoft.Build.NuGetSdkResolver.Test
{
    /// <summary>
    /// A mock implementation of <see cref="SdkLogger"/> that stores logged messages.
    /// </summary>
    public class MockSdkLogger : SdkLogger
    {
        /// <summary>
        /// Stores the list of messages that have been logged.
        /// </summary>
        private readonly List<KeyValuePair<string, MessageImportance>> _messages = new List<KeyValuePair<string, MessageImportance>>();

        /// <summary>
        /// Gets a list of messages that have been logged.
        /// </summary>
        public IReadOnlyCollection<KeyValuePair<string, MessageImportance>> LoggedMessages => _messages;

        /// <inheritdoc cref="LogMessage"/>
        public override void LogMessage(string message, MessageImportance messageImportance = MessageImportance.Low)
        {
            _messages.Add(new KeyValuePair<string, MessageImportance>(message, messageImportance));
        }
    }
}
