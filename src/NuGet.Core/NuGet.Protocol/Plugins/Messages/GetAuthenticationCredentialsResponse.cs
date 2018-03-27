// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;

namespace NuGet.Protocol.Plugins
{
    public class GetAuthenticationCredentialsResponse
    {
        public string Username { get; }

        public string Password { get; }

        public string Message { get; }

        /// <summary>
        /// Gets or sets the list of authentication types this credential is applicable to. Useful values include
        /// <c>basic</c>, <c>digest</c>, <c>negotiate</c>, and <c>ntlm</c>
        /// </summary>
        public IList<string> AuthTypes { get; }

        [JsonRequired]
        public MessageResponseCode ResponseCode { get; }


        [JsonConstructor]
        public GetAuthenticationCredentialsResponse(string username, string password, string message, IList<string> authTypes, MessageResponseCode responseCode)
        {

            if (!Enum.IsDefined(typeof(MessageResponseCode), responseCode))
            {
                throw new ArgumentException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.Plugin_UnrecognizedEnumValue,
                       nameof(responseCode)));
            }


            Username = username;
            Password = password;
            Message = message;
            AuthTypes = authTypes;
            ResponseCode = responseCode;
        }

        /// <summary>
        /// Gets a value indicating whether the provider returnd a valid response.
        /// </summary>
        /// <remarks>
        /// Either Username or Password (or both) must be set, and AuthTypes must either be null or contain at least
        /// one element
        /// </remarks>
        public bool IsValid => ResponseCode == MessageResponseCode.Success && (!string.IsNullOrWhiteSpace(Username) || !string.IsNullOrWhiteSpace(Password))
                               && (AuthTypes == null || AuthTypes.Any());
    }
}
