// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using NuGet.Versioning;

namespace NuGet.Protocol.Plugins
{
    /// <summary>
    /// Plugin connection options.
    /// </summary>
    public sealed class ConnectionOptions
    {
        /// <summary>
        /// Gets the plugin handshake timeout.
        /// </summary>
        public TimeSpan HandshakeTimeout { get; }

        /// <summary>
        /// Gets the minimum plugin protocol version.
        /// </summary>
        public SemanticVersion MinimumProtocolVersion { get; }

        /// <summary>
        /// Gets the plugin protocol version.
        /// </summary>
        public SemanticVersion ProtocolVersion { get; }

        /// <summary>
        /// Gets the plugin request timeout.
        /// </summary>
        public TimeSpan RequestTimeout { get; private set; }

        /// <summary>
        /// Instantiates a new <see cref="ConnectionOptions" /> class.
        /// </summary>
        /// <param name="protocolVersion">The plugin protocol version.</param>
        /// <param name="minimumProtocolVersion">The minimum plugin protocol version.</param>
        /// <param name="handshakeTimeout">The plugin handshake timeout.</param>
        /// <param name="requestTimeout">The plugin request timeout.</param>
        public ConnectionOptions(
            SemanticVersion protocolVersion,
            SemanticVersion minimumProtocolVersion,
            TimeSpan handshakeTimeout,
            TimeSpan requestTimeout)
        {
            if (protocolVersion == null)
            {
                throw new ArgumentNullException(nameof(protocolVersion));
            }

            if (minimumProtocolVersion == null)
            {
                throw new ArgumentNullException(nameof(minimumProtocolVersion));
            }

            if (minimumProtocolVersion > protocolVersion)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(protocolVersion),
                    protocolVersion,
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.Plugin_ProtocolVersionOutOfRange,
                        nameof(protocolVersion),
                        nameof(minimumProtocolVersion)));
            }

            if (!TimeoutUtilities.IsValid(handshakeTimeout))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(handshakeTimeout),
                    handshakeTimeout,
                    Strings.Plugin_TimeoutOutOfRange);
            }

            if (!TimeoutUtilities.IsValid(requestTimeout))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(requestTimeout),
                    requestTimeout,
                    Strings.Plugin_TimeoutOutOfRange);
            }

            ProtocolVersion = protocolVersion;
            MinimumProtocolVersion = minimumProtocolVersion;
            HandshakeTimeout = handshakeTimeout;
            RequestTimeout = requestTimeout;
        }

        /// <summary>
        /// Sets a new request timeout.
        /// </summary>
        /// <param name="requestTimeout">The new request timeout.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="requestTimeout" />
        /// is either less than <see cref="ProtocolConstants.MinTimeout" /> or greater than
        /// <see cref="ProtocolConstants.MaxTimeout" />.</exception>
        public void SetRequestTimeout(TimeSpan requestTimeout)
        {
            if (!TimeoutUtilities.IsValid(requestTimeout))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(requestTimeout),
                    requestTimeout,
                    Strings.Plugin_TimeoutOutOfRange);
            }

            RequestTimeout = requestTimeout;
        }

        /// <summary>
        /// Instantiates a <see cref="ConnectionOptions" /> class with default values.
        /// </summary>
        /// <returns>A <see cref="ConnectionOptions" />.</returns>
        public static ConnectionOptions CreateDefault()
        {
            return new ConnectionOptions(
                protocolVersion: ProtocolConstants.CurrentVersion,
                minimumProtocolVersion: ProtocolConstants.CurrentVersion,
                handshakeTimeout: TimeSpan.FromSeconds(10),
                requestTimeout: TimeSpan.FromSeconds(10));
        }
    }
}