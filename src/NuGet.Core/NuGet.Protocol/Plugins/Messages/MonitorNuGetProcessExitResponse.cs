﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using Newtonsoft.Json;

namespace NuGet.Protocol.Plugins
{
    /// <summary>
    /// A response to a monitor NuGet process exit request.
    /// </summary>
    public sealed class MonitorNuGetProcessExitResponse
    {
        /// <summary>
        /// Gets the response code.
        /// </summary>
        [JsonRequired]
        public MessageResponseCode ResponseCode { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MonitorNuGetProcessExitResponse" /> class.
        /// </summary>
        /// <param name="responseCode">The plugin's response code.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="responseCode" />
        /// is an undefined <see cref="MessageResponseCode" /> value.</exception>
        [JsonConstructor]
        public MonitorNuGetProcessExitResponse(MessageResponseCode responseCode)
        {
            if (!Enum.IsDefined(typeof(MessageResponseCode), responseCode))
            {
                throw new ArgumentException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.Plugin_UnrecognizedEnumValue,
                        responseCode),
                    nameof(responseCode));
            }

            ResponseCode = responseCode;
        }
    }
}