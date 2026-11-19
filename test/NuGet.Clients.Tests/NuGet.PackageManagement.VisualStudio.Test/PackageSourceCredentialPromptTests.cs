// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel;
using System.Net;
using System.Security;
using Xunit;

namespace NuGet.PackageManagement.VisualStudio.Test
{
    public class PackageSourceCredentialPromptTests
    {
        [Theory]
        [InlineData("https://unit.test/v3/index.json")]
        [InlineData("https://unit.test:8443/other/index.json")]
        public void PromptForCredentials_WithSavedCredentials_DoesNotPrompt(string source)
        {
            var savedCredentials = new NetworkCredential("user", "password");
            var prompt = new PackageSourceCredentialPrompt(
                target =>
                {
                    Assert.Equal("unit.test", target);
                    return savedCredentials;
                },
                UnexpectedPrompt,
                (_, _) => Assert.Fail("Saved credentials should not be written again."));

            NetworkCredential? result = prompt.PromptForCredentials(new Uri(source), isRetry: false, IntPtr.Zero);

            Assert.Same(savedCredentials, result);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void PromptForCredentials_PersistsOnlyWhenSelected(bool isRetry, bool save)
        {
            var credentials = new NetworkCredential("user", "password");
            int reads = 0;
            int writes = 0;
            var parentWindow = new IntPtr(123);
            var prompt = new PackageSourceCredentialPrompt(
                _ =>
                {
                    reads++;
                    return null;
                },
                (string caption, string message, string target, bool retry, IntPtr owner, out bool saveCredentials) =>
                {
                    Assert.Equal("unit.test", target);
                    Assert.Contains("https://unit.test:8443/v3/index.json", message);
                    Assert.DoesNotContain("secret", message);
                    Assert.Equal(isRetry, retry);
                    Assert.Equal(parentWindow, owner);
                    saveCredentials = save;
                    return credentials;
                },
                (target, value) =>
                {
                    Assert.Equal("unit.test", target);
                    Assert.Same(credentials, value);
                    writes++;
                });

            NetworkCredential? result = prompt.PromptForCredentials(
                new Uri("https://user:secret@unit.test:8443/v3/index.json?token=secret"),
                isRetry,
                parentWindow);

            Assert.Same(credentials, result);
            Assert.Equal(isRetry ? 0 : 1, reads);
            Assert.Equal(save ? 1 : 0, writes);
        }

        [Fact]
        public void PromptForCredentials_WhenCanceled_DoesNotSave()
        {
            var prompt = new PackageSourceCredentialPrompt(
                _ => null,
                (string caption, string message, string target, bool retry, IntPtr owner, out bool saveCredentials) =>
                {
                    saveCredentials = true;
                    return null;
                },
                (_, _) => Assert.Fail("Canceled credentials should not be saved."));

            Assert.Null(prompt.PromptForCredentials(new Uri("https://unit.test"), isRetry: false, IntPtr.Zero));
        }

        [Fact]
        public void PromptForCredentials_WhenSavingFails_ReportsError()
        {
            var error = new Win32Exception(5);
            var prompt = new PackageSourceCredentialPrompt(
                _ => null,
                (string caption, string message, string target, bool retry, IntPtr owner, out bool saveCredentials) =>
                {
                    saveCredentials = true;
                    return new NetworkCredential("user", "password");
                },
                (_, _) => throw error);

            Assert.Same(error, Assert.Throws<Win32Exception>(
                () => prompt.PromptForCredentials(new Uri("https://unit.test"), isRetry: false, IntPtr.Zero)));
        }

        [Theory]
        [InlineData("alice", true)]
        [InlineData("ALICE", true)]
        [InlineData("bob", false)]
        public void ReadCredentials_WithUnreadableDomainPassword_UsesFallbackOnlyForSameUser(
            string fallbackUser, bool shouldReturnFallback)
        {
            var fallback = new NetworkCredential(fallbackUser, "password");

            NetworkCredential? result = NativeMethods.ReadCredentials(
                "unit.test",
                (target, type) => target == "VSCredentials_unit.test"
                    ? fallback
                    : type == 2 ? new NetworkCredential("alice", string.Empty) : null);

            Assert.Same(shouldReturnFallback ? fallback : null, result);
        }

        [Theory]
        [InlineData(true, "unit.test\\user", "")]
        [InlineData(false, "user", "")]
        public void CreateStoredCredentials_PreservesNewUserNamesAndNormalizesLegacyUserNames(
            bool isPackageSourceCredential, string expectedUserName, string expectedDomain)
        {
            using var password = new SecureString();

            NetworkCredential result = NativeMethods.CreateStoredCredentials(
                "unit.test\\user", password, "unit.test", isPackageSourceCredential);

            Assert.Equal(expectedUserName, result.UserName);
            Assert.Equal(expectedDomain, result.Domain);
        }

        private static NetworkCredential? UnexpectedPrompt(
            string caption,
            string message,
            string target,
            bool retry,
            IntPtr owner,
            out bool saveCredentials)
        {
            saveCredentials = false;
            Assert.Fail("Saved credentials should be returned without showing the dialog.");
            return null;
        }

        [Fact]
        public void GetDisplayName_WithCredentialsAndQuery_DoesNotExposeSecrets()
        {
            var uri = new Uri("https://user:secret@unit.test/v3/index.json?token=secret");

            string source = PackageSourceCredentialPrompt.GetDisplayName(uri);

            Assert.Equal("https://unit.test/v3/index.json", source);
        }
    }
}
