// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;

namespace NuGet.Protocol.Plugins
{
    /// <summary>
    /// A response to a get credentials request.
    /// </summary>
    public sealed class GetCredentialsResponse
    {
        /// <summary>
        /// Gets the password.
        /// </summary>
        [System.Text.Json.Serialization.JsonPropertyName("password")]
        public string Password { get; init; }

        /// <summary>
        /// Gets the response code.
        /// </summary>
        [JsonRequired]
        [System.Text.Json.Serialization.JsonRequired]
        [System.Text.Json.Serialization.JsonPropertyName("responseCode")]
        public MessageResponseCode ResponseCode { get; init; }

        /// <summary>
        /// Gets the username.
        /// </summary>
        [System.Text.Json.Serialization.JsonPropertyName("username")]
        public string Username { get; init; }

        [System.Text.Json.Serialization.JsonPropertyName("authenticationTypes")]
        public IReadOnlyList<string> AuthenticationTypes { get; init; }


        /// <summary>
        /// Initializes a new instance of the <see cref="GetCredentialsResponse" /> class.
        /// </summary>
        /// <param name="responseCode">The plugin's response code.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="responseCode" />
        /// is an undefined <see cref="MessageResponseCode" /> value.</exception>
        [JsonConstructor]
        [System.Text.Json.Serialization.JsonConstructor]
        public GetCredentialsResponse(
            MessageResponseCode responseCode,
            string username,
            string password,
            IReadOnlyList<string> authenticationTypes = null)
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
            Username = username;
            Password = password;
            AuthenticationTypes = authenticationTypes;
        }
    }
}
