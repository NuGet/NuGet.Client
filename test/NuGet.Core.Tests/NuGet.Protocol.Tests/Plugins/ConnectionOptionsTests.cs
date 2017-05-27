// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Protocol.Plugins.Tests
{
    public class ConnectionOptionsTests
    {
        private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);
        private static readonly SemanticVersion _version1_0_0 = new SemanticVersion(major: 1, minor: 0, patch: 0);
        private static readonly SemanticVersion _version2_0_0 = new SemanticVersion(major: 2, minor: 0, patch: 0);

        [Fact]
        public void Constructor_ThrowsForNullProtocolVersion()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new ConnectionOptions(
                    protocolVersion: null,
                    minimumProtocolVersion: _version2_0_0,
                    handshakeTimeout: _timeout,
                    requestTimeout: _timeout));

            Assert.Equal("protocolVersion", exception.ParamName);
        }

        [Fact]
        public void Constructor_ThrowsForNullMinimumProtocolVersion()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new ConnectionOptions(
                    _version1_0_0,
                    minimumProtocolVersion: null,
                    handshakeTimeout: _timeout,
                    requestTimeout: _timeout));

            Assert.Equal("minimumProtocolVersion", exception.ParamName);
        }

        [Fact]
        public void Constructor_ThrowsForInvalidVersionRange()
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => new ConnectionOptions(_version1_0_0, _version2_0_0, _timeout, _timeout));

            Assert.Equal("protocolVersion", exception.ParamName);
            Assert.Equal(_version1_0_0, exception.ActualValue);
        }

        [Fact]
        public void Constructor_ThrowsForZeroHandshakeTimeout()
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => new ConnectionOptions(_version2_0_0, _version1_0_0, TimeSpan.Zero, _timeout));

            Assert.Equal("handshakeTimeout", exception.ParamName);
            Assert.Equal(TimeSpan.Zero, exception.ActualValue);
        }

        [Fact]
        public void Constructor_ThrowsForNegativeHandshakeTimeout()
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => new ConnectionOptions(_version2_0_0, _version1_0_0, TimeSpan.FromSeconds(-1), _timeout));

            Assert.Equal("handshakeTimeout", exception.ParamName);
            Assert.Equal(TimeSpan.FromSeconds(-1), exception.ActualValue);
        }

        [Fact]
        public void Constructor_ThrowsForTooLargeHandshakeTimeout()
        {
            var milliseconds = int.MaxValue + 1L;

            var exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => new ConnectionOptions(
                    _version2_0_0,
                    _version1_0_0,
                    TimeSpan.FromMilliseconds(milliseconds),
                    _timeout));

            Assert.Equal("handshakeTimeout", exception.ParamName);
            Assert.Equal(TimeSpan.FromMilliseconds(milliseconds), exception.ActualValue);
        }

        [Fact]
        public void Constructor_ThrowsForZeroRequestTimeout()
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => new ConnectionOptions(_version2_0_0, _version1_0_0, _timeout, TimeSpan.Zero));

            Assert.Equal("requestTimeout", exception.ParamName);
            Assert.Equal(TimeSpan.Zero, exception.ActualValue);
        }

        [Fact]
        public void Constructor_ThrowsForNegativeRequestTimeout()
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => new ConnectionOptions(_version2_0_0, _version1_0_0, _timeout, TimeSpan.FromSeconds(-1)));

            Assert.Equal("requestTimeout", exception.ParamName);
            Assert.Equal(TimeSpan.FromSeconds(-1), exception.ActualValue);
        }

        [Fact]
        public void Constructor_ThrowsForTooLargeRequestTimeout()
        {
            var milliseconds = int.MaxValue + 1L;

            var exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => new ConnectionOptions(
                    _version2_0_0,
                    _version1_0_0,
                    _timeout,
                    TimeSpan.FromMilliseconds(milliseconds)));

            Assert.Equal("requestTimeout", exception.ParamName);
            Assert.Equal(TimeSpan.FromMilliseconds(milliseconds), exception.ActualValue);
        }

        [Fact]
        public void Constructor_AcceptsEqualVersions()
        {
            var options = new ConnectionOptions(_version1_0_0, _version1_0_0, _timeout, _timeout);

            Assert.Equal(options.ProtocolVersion, options.MinimumProtocolVersion);
        }

        [Fact]
        public void Constructor_InitializesProperties()
        {
            var options = new ConnectionOptions(
                _version2_0_0,
                _version1_0_0,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(2));

            Assert.Equal(_version2_0_0, options.ProtocolVersion);
            Assert.Equal(_version1_0_0, options.MinimumProtocolVersion);
            Assert.Equal(TimeSpan.FromSeconds(1), options.HandshakeTimeout);
            Assert.Equal(TimeSpan.FromSeconds(2), options.RequestTimeout);
        }

        [Fact]
        public void CreateDefault_HasCorrectProtocolVersion()
        {
            var options = ConnectionOptions.CreateDefault();

            Assert.Equal(ProtocolConstants.CurrentVersion, options.ProtocolVersion);
        }

        [Fact]
        public void CreateDefault_HasCorrectMinimumProtocolVersion()
        {
            var options = ConnectionOptions.CreateDefault();

            Assert.Equal(ProtocolConstants.CurrentVersion, options.MinimumProtocolVersion);
        }

        [Fact]
        public void CreateDefault_HasCorrectHandshakeTimeout()
        {
            var options = ConnectionOptions.CreateDefault();

            Assert.Equal(ProtocolConstants.HandshakeTimeout, options.HandshakeTimeout);
        }

        [Fact]
        public void CreateDefault_HasCorrectRequestTimeoutDefault()
        {
            var options = ConnectionOptions.CreateDefault();

            Assert.Equal(ProtocolConstants.RequestTimeout, options.RequestTimeout);
        }

        [Fact]
        public void SetRequestTimeout_ThrowsForZeroTimeout()
        {
            var options = ConnectionOptions.CreateDefault();

            var exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => options.SetRequestTimeout(TimeSpan.Zero));

            Assert.Equal("requestTimeout", exception.ParamName);
            Assert.Equal(TimeSpan.Zero, exception.ActualValue);
        }

        [Fact]
        public void SetRequestTimeout_ThrowsForNegativeTimeout()
        {
            var options = ConnectionOptions.CreateDefault();

            var exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => options.SetRequestTimeout(TimeSpan.FromSeconds(-1)));

            Assert.Equal("requestTimeout", exception.ParamName);
            Assert.Equal(TimeSpan.FromSeconds(-1), exception.ActualValue);
        }

        [Fact]
        public void SetRequestTimeout_ThrowsForTooLargeTimeout()
        {
            var options = ConnectionOptions.CreateDefault();
            var milliseconds = int.MaxValue + 1L;

            var exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => options.SetRequestTimeout(TimeSpan.FromMilliseconds(milliseconds)));

            Assert.Equal("requestTimeout", exception.ParamName);
            Assert.Equal(TimeSpan.FromMilliseconds(milliseconds), exception.ActualValue);
        }

        [Fact]
        public void SetRequestTimeout_UpdatesRequestTimeout()
        {
            var options = ConnectionOptions.CreateDefault();

            options.SetRequestTimeout(ProtocolConstants.MaxTimeout);

            Assert.Equal(ProtocolConstants.MaxTimeout, options.RequestTimeout);
        }
    }
}