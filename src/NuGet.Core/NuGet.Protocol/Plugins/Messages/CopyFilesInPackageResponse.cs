﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;

namespace NuGet.Protocol.Plugins
{
    /// <summary>
    /// A response to a copy package files request.
    /// </summary>
    public sealed class CopyFilesInPackageResponse
    {
        /// <summary>
        /// Gets the paths of files copies.
        /// </summary>
        public IEnumerable<string> CopiedFiles { get; }

        /// <summary>
        /// Gets the response code.
        /// </summary>
        [JsonRequired]
        public MessageResponseCode ResponseCode { get; }

        /// <summary>
        /// Initializes a new <see cref="CopyFilesInPackageResponse" /> class.
        /// </summary>
        /// <param name="responseCode">The response code.</param>
        /// <param name="copiedFiles">The paths of files copies.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="responseCode" />
        /// is an undefined <see cref="MessageResponseCode" /> value.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="responseCode" /> 
        /// is <see cref="MessageResponseCode.Success" /> and <paramref name="copiedFiles" />
        /// is either <c>null</c> or empty.</exception>
        [JsonConstructor]
        public CopyFilesInPackageResponse(MessageResponseCode responseCode, IEnumerable<string> copiedFiles)
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

            if (responseCode == MessageResponseCode.Success && (copiedFiles == null || !copiedFiles.Any()))
            {
                throw new ArgumentException(Strings.ArgumentCannotBeNullOrEmpty, nameof(copiedFiles));
            }

            ResponseCode = responseCode;
            CopiedFiles = copiedFiles;
        }
    }
}