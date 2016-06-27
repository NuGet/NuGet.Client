// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Linq;


namespace NuGet.Credentials
{
    /// <summary>
    /// Wraps another ICredentials object by returning null if the authType is not in a specified allow list
    /// </summary>
    public class AuthTypeFilteredCredentials : ICredentials
    {
        private readonly IList<string> _authTypes;
        private readonly ICredentials _innerCredential;

        /// <summary>
        /// Initializes a new AuthTypeFilteredCredentials
        /// </summary>
        /// <param name="innerCredential">Credential to delegate to</param>
        /// <param name="authTypes">List of authTypes to respond to. May not be null or empty.</param>
        public AuthTypeFilteredCredentials(ICredentials innerCredential, IList<string> authTypes)
        {
            if (innerCredential == null)
            {
                throw new ArgumentNullException(nameof(innerCredential));
            }
            
            if (authTypes == null || !authTypes.Any())
            {
                throw new ArgumentException(Resources.Error_Argument_May_Not_Be_Null_Or_Empty, nameof(authTypes));
            }

            _innerCredential = innerCredential;
            _authTypes = authTypes;
        }

        public NetworkCredential GetCredential(Uri uri, string authType)
        {
            return _authTypes.Any(x => StringComparer.OrdinalIgnoreCase.Equals(x, authType))
                ? _innerCredential.GetCredential(uri, authType)
                : null;
        }
    }
}