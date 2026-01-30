// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Text.Json.Serialization;

namespace NuGet.Protocol.Plugins
{
    /// <summary>
    /// A notification indicating the sender has experienced an unrecoverable fault.
    /// </summary>
    public sealed class Fault
    {
        /// <summary>
        /// Gets the fault message.
        /// </summary>
        [Newtonsoft.Json.JsonRequired]
        [JsonPropertyName("Message")]
        [System.Text.Json.Serialization.JsonRequired]
        public string Message { get; init; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Fault" /> class.
        /// </summary>
        /// <param name="message">The fault message.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="message" />
        /// is either <see langword="null" /> or an empty string.</exception>
        [Newtonsoft.Json.JsonConstructor]
        [System.Text.Json.Serialization.JsonConstructor]
        public Fault(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                throw new ArgumentException(Strings.ArgumentCannotBeNullOrEmpty, nameof(message));
            }

            Message = message;
        }
    }
}
