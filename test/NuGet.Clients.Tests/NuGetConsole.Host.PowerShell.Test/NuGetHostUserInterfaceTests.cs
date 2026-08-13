// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using Moq;
using NuGet.VisualStudio;
using NuGetConsole.Host.PowerShell.Implementation;
using Test.Utility.Threading;
using Xunit;

namespace NuGetConsole.Host.PowerShell.Test
{
    [Collection(DispatcherThreadCollection.CollectionName)]
    public class NuGetHostUserInterfaceTests
    {
        public NuGetHostUserInterfaceTests(DispatcherThreadFixture fixture)
        {
            NuGetUIThreadHelper.SetCustomJoinableTaskFactory(fixture.JoinableTaskFactory);
        }

        [Theory]
        [InlineData(0x20000)]
        [InlineData(0x2A6B2)]
        [InlineData(0x1F600)]
        [InlineData(0xE000)]
        [InlineData(0x100000)]
        public void ReadLine_BackspaceRemovesCompleteCharacter(int codePoint)
        {
            var keys = new Queue<VsKeyInfo>();
            keys.Enqueue(CreateKey('A'));
            keys.Enqueue(CreateKey('B'));
            keys.Enqueue(CreateKey('C'));

            foreach (char character in char.ConvertFromUtf32(codePoint))
            {
                keys.Enqueue(CreateKey(character));
            }

            keys.Enqueue(CreateKey('\b', virtualKey: 8));
            keys.Enqueue(VsKeyInfo.Enter);

            var dispatcher = new Mock<IConsoleDispatcher>();
            dispatcher.Setup(x => x.WaitKey()).Returns(() => keys.Dequeue());

            var console = new TestConsole(dispatcher.Object);

            var host = new NuGetPSHost("test")
            {
                ActiveConsole = console
            };
            var target = new TestNuGetHostUserInterface(host);

            string result = target.ReadLine();

            Assert.Equal(3, result.Length);
            Assert.Equal("ABC", result);
            Assert.Equal(1, console.WriteBackspaceCount);
        }

        private static VsKeyInfo CreateKey(char character, byte virtualKey = 0)
        {
            return VsKeyInfo.Create(
                Key.None,
                character,
                virtualKey,
                keyStates: KeyStates.Down);
        }

        private sealed class TestConsole : IConsole
        {
            public TestConsole(IConsoleDispatcher dispatcher)
            {
                Dispatcher = dispatcher;
                Host = Mock.Of<IHost>();
            }

            public int WriteBackspaceCount { get; private set; }
            public IHost Host { get; set; }
            public bool ShowDisclaimerHeader => false;
            public IConsoleDispatcher Dispatcher { get; }
            public int ConsoleWidth => 80;

            public Task ActivateAsync() => Task.CompletedTask;
            public Task ClearAsync() => Task.CompletedTask;
            public Task WriteProgressAsync(string currentOperation, int percentComplete) => Task.CompletedTask;
            public Task WriteAsync(string text) => Task.CompletedTask;
            public Task WriteLineAsync(string text) => Task.CompletedTask;
            public Task WriteLineAsync(string format, params object[] args) => Task.CompletedTask;
            public Task WriteAsync(string text, Color? foreground, Color? background) => Task.CompletedTask;

            public Task WriteBackspaceAsync()
            {
                WriteBackspaceCount++;
                return Task.CompletedTask;
            }
        }

        private sealed class TestNuGetHostUserInterface : NuGetHostUserInterface
        {
            public TestNuGetHostUserInterface(NuGetPSHost host)
                : base(host)
            {
            }

            public override void Write(string value)
            {
            }

            public override void WriteLine(string value)
            {
            }
        }
    }
}
