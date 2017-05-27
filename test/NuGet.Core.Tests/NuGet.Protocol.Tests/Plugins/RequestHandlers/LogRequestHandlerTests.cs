// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Common;
using Xunit;

namespace NuGet.Protocol.Plugins.Tests
{
    public class LogRequestHandlerTests
    {
        private readonly Mock<ILogger> _logger;
        private readonly Mock<IResponseHandler> _responseHandler;

        public LogRequestHandlerTests()
        {
            _logger = new Mock<ILogger>(MockBehavior.Strict);
            _responseHandler = new Mock<IResponseHandler>(MockBehavior.Strict);
        }

        [Fact]
        public void Constructor_ThrowsForNullLogger()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new LogRequestHandler(logger: null, logLevel: LogLevel.Debug));

            Assert.Equal("logger", exception.ParamName);
        }

        [Fact]
        public void CancellationToken_IsNone()
        {
            var handler = new LogRequestHandler(_logger.Object, LogLevel.Debug);

            Assert.Equal(CancellationToken.None, handler.CancellationToken);
        }

        [Fact]
        public async Task HandleCancelAsync_Throws()
        {
            var handler = new LogRequestHandler(_logger.Object, LogLevel.Debug);
            var request = new Message(
                requestId: "a",
                type: MessageType.Cancel,
                method: MessageMethod.Log);

            await Assert.ThrowsAsync<NotSupportedException>(
                () => handler.HandleCancelAsync(request, CancellationToken.None));
        }

        [Fact]
        public async Task HandleProgressAsync_Throws()
        {
            var handler = new LogRequestHandler(_logger.Object, LogLevel.Debug);
            var request = new Message(
                requestId: "a",
                type: MessageType.Progress,
                method: MessageMethod.Log);

            await Assert.ThrowsAsync<NotSupportedException>(
                () => handler.HandleProgressAsync(request, CancellationToken.None));
        }

        [Fact]
        public async Task HandleResponseAsync_ThrowsForNullRequest()
        {
            var handler = new LogRequestHandler(_logger.Object, LogLevel.Debug);

            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => handler.HandleResponseAsync(
                    request: null,
                    responseHandler: Mock.Of<IResponseHandler>(),
                    cancellationToken: CancellationToken.None));

            Assert.Equal("request", exception.ParamName);
        }

        [Fact]
        public async Task HandleResponseAsync_ThrowsForNullResponseHandler()
        {
            var handler = new LogRequestHandler(_logger.Object, LogLevel.Debug);
            var request = new Message(
                requestId: "a",
                type: MessageType.Request,
                method: MessageMethod.Log);

            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => handler.HandleResponseAsync(
                    request,
                    responseHandler: null,
                    cancellationToken: CancellationToken.None));

            Assert.Equal("responseHandler", exception.ParamName);
        }

        [Fact]
        public async Task HandleResponseAsync_ThrowsIfCancelled()
        {
            var handler = new LogRequestHandler(_logger.Object, LogLevel.Debug);
            var request = new Message(
                requestId: "a",
                type: MessageType.Request,
                method: MessageMethod.Log);

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => handler.HandleResponseAsync(
                    request,
                    Mock.Of<IResponseHandler>(),
                    new CancellationToken(canceled: true)));
        }

        [Fact]
        public async Task HandleResponseAsync_LogsDebug()
        {
            await HandleResponseAsync(
                LogLevel.Debug,
                LogLevel.Debug,
                MessageResponseCode.Success,
                (message) => logger => logger.LogDebug(It.Is<string>(m => m == message)));
        }

        [Fact]
        public async Task HandleResponseAsync_LogsVerbose()
        {
            await HandleResponseAsync(
                LogLevel.Verbose,
                LogLevel.Verbose,
                MessageResponseCode.Success,
                (message) => logger => logger.LogVerbose(It.Is<string>(m => m == message)));
        }

        [Fact]
        public async Task HandleResponseAsync_LogsInformation()
        {
            await HandleResponseAsync(
                LogLevel.Information,
                LogLevel.Information,
                MessageResponseCode.Success,
                (message) => logger => logger.LogInformation(It.Is<string>(m => m == message)));
        }

        [Fact]
        public async Task HandleResponseAsync_LogsMinimal()
        {
            await HandleResponseAsync(
                LogLevel.Minimal,
                LogLevel.Minimal,
                MessageResponseCode.Success,
                (message) => logger => logger.LogMinimal(It.Is<string>(m => m == message)));
        }

        [Fact]
        public async Task HandleResponseAsync_LogsWarning()
        {
            await HandleResponseAsync(
                LogLevel.Warning,
                LogLevel.Warning,
                MessageResponseCode.Success,
                (message) => logger => logger.LogWarning(It.Is<string>(m => m == message)));
        }

        [Fact]
        public async Task HandleResponseAsync_LogsError()
        {
            await HandleResponseAsync(
                LogLevel.Error,
                LogLevel.Error,
                MessageResponseCode.Success,
                (message) => logger => logger.LogError(It.Is<string>(m => m == message)));
        }

        [Theory]
        [InlineData(LogLevel.Verbose)]
        [InlineData(LogLevel.Information)]
        [InlineData(LogLevel.Minimal)]
        [InlineData(LogLevel.Warning)]
        [InlineData(LogLevel.Error)]
        public async Task HandleResponseAsync_FailsOnRequestWithMoreDetailedLogLevel(LogLevel handlerLogLevel)
        {
            var requestedLogLevel = handlerLogLevel - 1;

            await HandleResponseAsync(
                handlerLogLevel,
                requestedLogLevel,
                MessageResponseCode.Error,
                expressionFunc: null);
        }

        private async Task HandleResponseAsync(
            LogLevel handlerLogLevel,
            LogLevel requestLogLevel,
            MessageResponseCode responseCode,
            Func<string, Expression<Action<ILogger>>> expressionFunc)
        {
            var handler = new LogRequestHandler(_logger.Object, handlerLogLevel);
            var payload = new LogRequest(requestLogLevel, message: "a");
            var request = MessageUtilities.Create(
                requestId: "b",
                type: MessageType.Request,
                method: MessageMethod.Log,
                payload: payload);

            _responseHandler.Setup(x => x.SendResponseAsync(
                    It.Is<Message>(message => message == request),
                    It.Is<LogResponse>(response => response.ResponseCode == responseCode),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(0));

            if (expressionFunc != null)
            {
                var expression = expressionFunc(payload.Message);

                _logger.Setup(expression);
            }

            await handler.HandleResponseAsync(request, _responseHandler.Object, CancellationToken.None);

            if (expressionFunc == null)
            {
                _logger.Verify();
            }
            else
            {
                var expression = expressionFunc(payload.Message);

                _logger.Verify(expression, Times.Once);
            }
        }
    }
}