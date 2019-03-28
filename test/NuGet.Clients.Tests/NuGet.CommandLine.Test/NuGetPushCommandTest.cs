// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Principal;
using System.Text;
using NuGet.Configuration;
using NuGet.Test.Utility;
using Xunit;
using NuGet.Common;

namespace NuGet.CommandLine.Test
{
    public class NuGetPushCommandTest
    {
        private const string ApiKeyHeader = "X-NuGet-ApiKey";
        private static readonly string NuGetExePath = Util.GetNuGetExePath();

        private readonly string _originalCredentialProvidersEnvar =
            Environment.GetEnvironmentVariable(ExtensionLocator.CredentialProvidersEnvar);

        // Tests pushing to a source that is a v2 file system directory.
        [Fact]
        public void PushCommand_PushToV2FileSystemSource()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var packageDirectory = TestDirectory.Create())
            using (var source = TestDirectory.Create())
            {
                // Arrange
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);

                // Act
                string[] args = new string[] { "push", packageFileName, "-Source", source };
                var result = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    string.Join(" ", args),
                    true);

                // Assert
                Assert.Equal(0, result.Item1);
                Assert.True(File.Exists(Path.Combine(source, "testPackage1.1.1.0.nupkg")));
                var output = result.Item2;
                Assert.DoesNotContain("WARNING: No API Key was provided", output);
            }
        }

        [Fact]
        public void PushCommand_PushToV3FileSystemSource()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var packageDirectory = TestDirectory.Create())
            using (var source = TestDirectory.Create())
            {
                //drop dummy artifacts to make it a V3
                var dummyPackageName = "foo";
                Directory.CreateDirectory(Path.Combine(source.Path, dummyPackageName));
                var f = Directory.CreateDirectory(Path.Combine(source.Path, dummyPackageName, "1.0.0"));
                File.WriteAllText(Path.Combine(f.FullName, dummyPackageName + ".nuspec"), "some text");

                // Arrange
                var version = "1.1.0";
                var packageId = "testPackage1";
                var packageFileName = Util.CreateTestPackage(packageId, version, packageDirectory);

                // Act
                string[] args = new string[] { "push", packageFileName, "-Source", source };
                var result = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    string.Join(" ", args),
                    true);

                // Assert
                Assert.Equal(0, result.Item1);
                var basename = string.Format("{0}.{1}.", packageId, version);
                var baseFolder = Path.Combine(packageId, version) + Path.DirectorySeparatorChar;
                Assert.True(File.Exists(Path.Combine(source, baseFolder + packageId + ".nuspec")));
                Assert.True(File.Exists(Path.Combine(source, baseFolder + basename + "nupkg")));
                Assert.True(File.Exists(Path.Combine(source, baseFolder + basename + "nupkg.sha512")));
            }
        }

        [Fact]
        public void PushCommand_PushToV2AbsoluteFileSystemDefaultPushSource()
        {
            string nugetexe = Util.GetNuGetExePath();

            using (TestDirectory packageDirectory = TestDirectory.Create())
            using (TestDirectory source = TestDirectory.Create())
            {
                // Arrange
                string packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                string config = $@"<?xml version='1.0' encoding='utf-8'?>
<configuration>
  <config>
    <add key='DefaultPushSource' value='{source}' />
  </config>
</configuration>";

                string configFileName = Path.Combine(packageDirectory, "nuget.config");
                File.WriteAllText(configFileName, config);

                // Act
                string[] args = new string[] { "push", packageFileName };
                var result = CommandRunner.Run(
                    nugetexe,
                    packageDirectory,
                    string.Join(" ", args),
                    true);

                // Assert
                Assert.Equal(0, result.Item1);
                Assert.True(File.Exists(Path.Combine(source, "testPackage1.1.1.0.nupkg")));
                var output = result.Item2;
                Assert.DoesNotContain("WARNING: No API Key was provided", output);
            }
        }

        [Fact]
        public void PushCommand_PushToV2RelativeFileSystemDefaultPushSource()
        {
            string nugetexe = Util.GetNuGetExePath();

            using (var root = TestDirectory.Create())
            {
                var packageDirectory = Path.Combine(root, "packages");
                var source = Path.Combine(root, "source");
                Directory.CreateDirectory(packageDirectory);
                Directory.CreateDirectory(source);

                // Arrange
                string packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                string config = $@"<?xml version='1.0' encoding='utf-8'?>
<configuration>
  <config>
    <add key='DefaultPushSource' value='..\{Path.GetFileName(source)}' />
  </config>
</configuration>";

                string configFileName = Path.Combine(packageDirectory, "nuget.config");
                File.WriteAllText(configFileName, config);

                // Act
                string[] args = new string[] { "push", packageFileName };
                var result = CommandRunner.Run(
                    nugetexe,
                    packageDirectory,
                    string.Join(" ", args),
                    true);

                // Assert
                Assert.Equal(0, result.Item1);
                Assert.True(File.Exists(Path.Combine(source, "testPackage1.1.1.0.nupkg")));
            }
        }

        [Fact]
        public void PushCommand_PushToV2NamedDefaultPushSource()
        {
            string nugetexe = Util.GetNuGetExePath();

            using (TestDirectory packageDirectory = TestDirectory.Create())
            using (TestDirectory source = TestDirectory.Create())
            {
                // Arrange
                string packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                string config = $@"<?xml version='1.0' encoding='utf-8'?>
<configuration>
  <config>
    <add key='DefaultPushSource' value='name' />
  </config>
  <packageSources>
    <add key='name' value='{source}' />
  </packageSources>
</configuration>";

                string configFileName = Path.Combine(packageDirectory, "nuget.config");
                File.WriteAllText(configFileName, config);

                // Act
                string[] args = new string[] { "push", packageFileName };
                var result = CommandRunner.Run(
                    nugetexe,
                    packageDirectory,
                    string.Join(" ", args),
                    true);

                // Assert
                Assert.Equal(0, result.Item1);
                Assert.True(File.Exists(Path.Combine(source, "testPackage1.1.1.0.nupkg")));
            }
        }

        // Same as PushCommand_PushToFileSystemSource, except that the directory is specified
        // in unix style.
        [Fact]
        public void PushCommand_PushToFileSystemSourceUnixStyle()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var packageDirectory = TestDirectory.Create())
            using (var windowsSource = TestDirectory.Create())
            {
                // Arrange
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);

                var source = ((string)windowsSource).Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                // Act
                string[] args = new string[] { "push", packageFileName, "-Source", source };
                var result = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    string.Join(" ", args),
                    true);

                // Assert
                Assert.Equal(0, result.Item1);
                Assert.True(File.Exists(Path.Combine(source, "testPackage1.1.1.0.nupkg")));
            }
        }

        // Same as PushCommand_PushToFileSystemSource, except that the directory is specified
        // in UNC format.
        [WindowsNTFact]
        public void PushCommand_PushToFileSystemSourceUncStyle()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var packageDirectory = TestDirectory.Create())
            using (var source = TestDirectory.Create())
            {
                // Arrange
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                var uncSource = @"\\localhost\" + ((string)source).Replace(':', '$');

                // Act
                string[] args = new string[] { "push", packageFileName, "-Source", uncSource };
                var result = CommandRunner.Run(
                                nugetexe,
                                Directory.GetCurrentDirectory(),
                                string.Join(" ", args),
                                true);

                // Assert
                Assert.Equal(0, result.Item1);
                Assert.True(File.Exists(Path.Combine(source, "testPackage1.1.1.0.nupkg")));
            }
        }

        // Tests pushing to an http source
        [Fact]
        public void PushCommand_PushToServer()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var packageDirectory = TestDirectory.Create())
            {
                // Arrange
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                string outputFileName = Path.Combine(packageDirectory, "t1.nupkg");

                using (var server = new MockServer())
                {
                    server.Get.Add("/push", r => "OK");
                    server.Put.Add("/push", r =>
                    {
                        byte[] buffer = MockServer.GetPushedPackage(r);
                        using (var of = new FileStream(outputFileName, FileMode.Create))
                        {
                            of.Write(buffer, 0, buffer.Length);
                        }

                        return HttpStatusCode.Created;
                    });
                    server.Start();

                    // Act
                    var result = CommandRunner.Run(
                                    nugetexe,
                                    Directory.GetCurrentDirectory(),
                                    $"push {packageFileName} -Source {server.Uri}push",
                                    true);
                    server.Stop();

                    // Assert
                    Assert.True(0 == result.Item1, $"{result.Item2} {result.Item3}");
                    var output = result.Item2;
                    Assert.Contains("Your package was pushed.", output);
                    AssertFileEqual(packageFileName, outputFileName);
                }
            }
        }

        [Theory]
        [MemberData(nameof(ServerWarningData))]
        public void PushCommand_LogsServerWarningsWhenPresent(string firstServerWarning, string secondServerWarning)
        {
            var serverWarnings = new[] { firstServerWarning, secondServerWarning };
            var nugetexe = Util.GetNuGetExePath();

            // Arrange
            using (var packageDirectory = TestDirectory.Create())
            {
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                using (var server = new MockServer())
                {
                    server.Get.Add("/push", r => "OK");
                    server.Put.Add("/push", r => HttpStatusCode.Created);

                    server.AddServerWarnings(serverWarnings);

                    server.Start();

                    // Act
                    var args = new string[]
                    {"push", packageFileName, "-Source", server.Uri + "push", "-Apikey", "token"};
                    var result = CommandRunner.Run(
                        nugetexe,
                        Directory.GetCurrentDirectory(),
                        string.Join(" ", args),
                        true);
                    server.Stop();

                    // Assert
                    var output = result.Item2;
                    foreach (var serverWarning in serverWarnings)
                    {
                        if (!string.IsNullOrEmpty(serverWarning))
                        {
                            Assert.Contains(serverWarning, output);
                        }
                    }
                }
            }
        }

        [Fact]
        public void PushCommand_PushToServerNoSymbols()
        {
            // Test pushing to an http source, but leaving out the symbols package (-NoSymbols).
            // The symbols package would try to get pushed to a public symbols server if
            // the -NoSymbols switch wasn't set.

            using (TestDirectory packageDirectory = TestDirectory.Create())
            using (MockServer server = new MockServer())
            {
                // Arrange
                string packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                string symbolFileName = packageFileName.Replace(".nupkg", ".symbols.nupkg");
                File.WriteAllText(symbolFileName, "This must be invalid so symbols would fail if they were actually pushed");

                server.Get.Add("/push", r => "OK");
                server.Put.Add("/push", r => HttpStatusCode.Created);
                server.Start();

                // Act
                CommandRunnerResult result = CommandRunner.Run(
                    Util.GetNuGetExePath(),
                    Directory.GetCurrentDirectory(),
                    $"push {packageFileName} -Source {server.Uri}push -NoSymbols",
                    waitForExit: true);

                // Assert
                Assert.Equal(0, result.Item1);
                Assert.Contains("Your package was pushed.", result.Item2);
                Assert.DoesNotContain("symbol", result.Item2);
                Assert.DoesNotContain(NuGetConstants.DefaultSymbolServerUrl, result.Item2);
            }
        }

        [Fact]
        public void PushCommand_PushToServerWithSymbols()
        {
            using (var packageDirectory = TestDirectory.Create())
            using (var server = new MockServer())
            {
                // Arrange
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                var symbolFileName = packageFileName.Replace(".nupkg", ".symbols.nupkg");
                File.Copy(packageFileName, symbolFileName);

                server.Get.Add("/push", r => "OK");
                server.Put.Add("/push", r =>
                {
                    return r.Headers["X-NuGet-ApiKey"] == "PushKey"
                        ? HttpStatusCode.Created
                        : HttpStatusCode.Unauthorized;
                });

                server.Get.Add("/symbols", r => "OK");
                server.Put.Add("/symbols", r =>
                {
                    return r.Headers["X-NuGet-ApiKey"] == "PushSymbolsKey"
                        ? HttpStatusCode.Created
                        : HttpStatusCode.Unauthorized;
                });

                server.Start();

                var pushUri = $"{server.Uri}push";
                var pushSymbolsUri = $"{server.Uri}symbols";

                // Act
                CommandRunnerResult result = CommandRunner.Run(
                    Util.GetNuGetExePath(),
                    Directory.GetCurrentDirectory(),
                    $"push {packageFileName} -Source {pushUri} -SymbolSource {pushSymbolsUri} -ApiKey PushKey -SymbolApiKey PushSymbolsKey",
                    waitForExit: true);

                // Assert
                Assert.Equal(0, result.Item1);
                Assert.Contains($"Pushing testPackage1.1.1.0.nupkg to '{pushUri}'", result.Item2);
                Assert.Contains($"Created {pushUri}", result.Item2);
                Assert.Contains($"Pushing testPackage1.1.1.0.symbols.nupkg to '{pushSymbolsUri}'", result.Item2);
                Assert.Contains($"Created {pushSymbolsUri}", result.Item2);
                Assert.Contains("Your package was pushed.", result.Item2);
            }
        }

        [Fact]
        public void PushCommand_PushToDirectoryWithSymbols()
        {
            using (var packageDirectory = TestDirectory.Create())
            {
                // Arrange
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                var symbolFileName = packageFileName.Replace(".nupkg", ".symbols.nupkg");
                File.Copy(packageFileName, symbolFileName);

                var pushSource = Path.Combine(packageDirectory, "source");
                var pushSymbolsSource = Path.Combine(packageDirectory, "symbols");
                Directory.CreateDirectory(pushSource);
                Directory.CreateDirectory(pushSymbolsSource);

                // Act
                CommandRunnerResult result = CommandRunner.Run(
                    Util.GetNuGetExePath(),
                    Directory.GetCurrentDirectory(),
                    $"push {packageFileName} -Source {pushSource} -SymbolSource {pushSymbolsSource}",
                    waitForExit: true);

                // Assert
                Assert.Equal(0, result.Item1);
                Assert.Contains($"Pushing testPackage1.1.1.0.nupkg to '{pushSource}'", result.Item2);
                Assert.Contains($"Pushing testPackage1.1.1.0.symbols.nupkg to '{pushSymbolsSource}'", result.Item2);
                Assert.Contains("Your package was pushed.", result.Item2);
            }
        }

        [Fact]
        public void PushCommand_PushToDirectoryInConfigWithSymbols()
        {
            using (var packageDirectory = TestDirectory.Create())
            {
                // Arrange
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                var symbolFileName = packageFileName.Replace(".nupkg", ".symbols.nupkg");
                var configFileName = Path.Combine(packageDirectory, "nuget.config");
                File.Copy(packageFileName, symbolFileName);

                var pushSource = Path.Combine(packageDirectory, "source");
                var pushSymbolsSource = Path.Combine(packageDirectory, "symbols");
                Directory.CreateDirectory(pushSource);
                Directory.CreateDirectory(pushSymbolsSource);

                var config = $@"<?xml version='1.0' encoding='utf-8'?>
<configuration>
    <packageSources>
        <clear />
        <add key='pushSource' value='{pushSource}' />
        <add key='pushSymbolsSource' value='{pushSymbolsSource}' />
    </packageSources>
</configuration>";
                File.WriteAllText(configFileName, config);

                // Act
                CommandRunnerResult result = CommandRunner.Run(
                    Util.GetNuGetExePath(),
                    packageDirectory,
                    $"push {packageFileName} -Source pushSource -SymbolSource pushSymbolsSource",
                    waitForExit: true);

                // Assert
                Assert.Equal(0, result.Item1);
                Assert.Contains($"Pushing testPackage1.1.1.0.nupkg to '{pushSource}'", result.Item2);
                Assert.Contains($"Pushing testPackage1.1.1.0.symbols.nupkg to '{pushSymbolsSource}'", result.Item2);
                Assert.Contains("Your package was pushed.", result.Item2);
            }
        }

        [Fact]
        public void PushCommand_PushTimeoutErrorMessage()
        {
            using (TestDirectory packageDirectory = TestDirectory.Create())
            using (MockServer server = new MockServer())
            {
                // Arrange
                string packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                server.Get.Add("/push", r => "OK");
                server.Put.Add("/push", r =>
                {
                    System.Threading.Thread.Sleep(2000);
                    return HttpStatusCode.Created;
                });
                server.Start();

                // Act
                CommandRunnerResult result = CommandRunner.Run(
                    Util.GetNuGetExePath(),
                    Directory.GetCurrentDirectory(),
                    $"push {packageFileName} -Source {server.Uri}push -Timeout 1",
                    waitForExit: true);

                // Assert
                Assert.Equal(1, result.Item1);
                Assert.Contains("took too long", result.Item3);
            }
        }

        // Tests that push command can follow redirection correctly.
        [SkipMono]
        public void PushCommand_PushToServerFollowRedirection()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var packageDirectory = TestDirectory.Create())
            {
                // Arrange
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                string outputFileName = Path.Combine(packageDirectory, "t1.nupkg");

                using (var server = new MockServer())
                {
                    server.Get.Add("/redirect", r => "OK");
                    server.Put.Add("/redirect", r =>
                        new Action<HttpListenerResponse>(
                            res =>
                            {
                                res.Redirect(server.Uri + "nuget");
                            }));
                    server.Put.Add("/nuget", r =>
                    {
                        byte[] buffer = MockServer.GetPushedPackage(r);
                        using (var of = new FileStream(outputFileName, FileMode.Create))
                        {
                            of.Write(buffer, 0, buffer.Length);
                        }

                        return HttpStatusCode.Created;
                    });
                    server.Start();

                    // Act
                    string[] args = new string[] { "push", packageFileName, "-Source", server.Uri + "redirect" };
                    var result = CommandRunner.Run(
                        nugetexe,
                        Directory.GetCurrentDirectory(),
                        string.Join(" ", args),
                        true);
                    server.Stop();

                    // Assert
                    var output = result.Item2;
                    Assert.True(0 == result.Item1, result.Item2 + " " + result.Item3);
                    Assert.Contains("Your package was pushed.", output);
                    AssertFileEqual(packageFileName, outputFileName);
                }
            }
        }

        // Tests that push command will terminate even when there is an infinite
        // redirection loop.
        [Fact(Skip = "On-hold, nuget.org has removed the redirect long ago")]
        public void PushCommand_PushToServerWithInfiniteRedirectionLoop()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var packageDirectory = TestDirectory.Create())
            {
                // Arrange
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);

                using (var server = new MockServer())
                {
                    server.Get.Add("/redirect", r => "OK");
                    server.Put.Add("/redirect", r =>
                        new Action<HttpListenerResponse>(
                            res =>
                            {
                                res.Redirect(server.Uri + "redirect");
                            }));
                    server.Start();

                    // Act
                    string[] args = new string[] { "push", packageFileName, "-Source", server.Uri + "redirect" };
                    var result = CommandRunner.Run(
                        nugetexe,
                        Directory.GetCurrentDirectory(),
                        string.Join(" ", args),
                        true);
                    server.Stop();

                    // Assert
                    Assert.NotEqual(0, result.Item1);
                    Assert.Contains("Too many automatic redirections were attempted.", result.Item3);
                }
            }
        }

        // Tests that push command generates error when it detects invalid redirection location.
        [Fact(Skip = "On-hold, nuget.org has removed the redirect long ago")]
        public void PushCommand_PushToServerWithInvalidRedirectionLocation()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var packageDirectory = TestDirectory.Create())
            {
                // Arrange
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);

                using (var server = new MockServer())
                {
                    server.Get.Add("/redirect", r => "OK");
                    server.Put.Add("/redirect", r => HttpStatusCode.Redirect);
                    server.Start();

                    // Act
                    string[] args = new string[] { "push", packageFileName, "-Source", server.Uri + "redirect" };
                    var result = CommandRunner.Run(
                        nugetexe,
                        Directory.GetCurrentDirectory(),
                        string.Join(" ", args),
                        true);
                    server.Stop();

                    // Assert
                    Assert.NotEqual(0, result.Item1);
                    Assert.Contains("The remote server returned an error: (302)", result.Item3);
                }
            }
        }

        // Regression test for the bug that "nuget.exe push" will retry forever instead of asking for
        // user's password when NuGet.Server uses Windows Authentication.
        [Fact(Skip = "TODO: reconstruct faked response headers which won't crash HttpClient. " +
            "Using real server, same scenario works fine")]
        public void PushCommand_PushToServerWontRetryForever()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var packageDirectory = TestDirectory.Create())
            {
                // Arrange
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                string outputFileName = Path.Combine(packageDirectory, "t1.nupkg");

                using (var server = new MockServer())
                {
                    server.Get.Add("/push", r => "OK");
                    server.Put.Add("/push", r => new Action<HttpListenerResponse>(
                        response =>
                        {
                            response.AddHeader("WWW-Authenticate", "NTLM");
                            response.StatusCode = (int)HttpStatusCode.Unauthorized;
                        }));
                    server.Start();

                    // Act
                    var args = "push " + packageFileName +
                        " -Source " + server.Uri + "push -NonInteractive -ApiKey FooBar";
                    var r1 = CommandRunner.Run(
                        nugetexe,
                        packageDirectory,
                        args,
                        waitForExit: true,
                        timeOutInMilliseconds: 10000);
                    server.Stop();

                    // Assert
                    Assert.NotEqual(0, r1.Item1);
                    Assert.Contains("Please provide credentials for:", r1.Item2);
                    Assert.Contains("UserName:", r1.Item2);
                }
            }
        }

        // Test push command to a server using basic authentication.
        [SkipMono]
        public void PushCommand_PushToServerBasicAuth()
        {
            var nugetexe = Util.GetNuGetExePath();

            List<string> credentialForGetRequest = new List<string>();
            List<string> credentialForPutRequest = new List<string>();

            using (var packageDirectory = TestDirectory.Create())
            {
                // Arrange
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                string outputFileName = Path.Combine(packageDirectory, "t1.nupkg");

                using (var server = new MockServer())
                {
                    server.Listener.AuthenticationSchemes = AuthenticationSchemes.Basic;
                    server.Put.Add("/nuget", r => new Action<HttpListenerResponse>(res =>
                    {
                        var h = r.Headers["Authorization"];
                        var credential = Encoding.Default.GetString(Convert.FromBase64String(h.Substring(6)));
                        credentialForPutRequest.Add(credential);

                        if (credential.Equals("testuser:testpassword", StringComparison.OrdinalIgnoreCase))
                        {
                            res.StatusCode = (int)HttpStatusCode.OK;
                        }
                        else
                        {
                            res.AddHeader("WWW-Authenticate", "Basic ");
                            res.StatusCode = (int)HttpStatusCode.Unauthorized;
                        }
                    }));
                    server.Start();

                    // Act
                    var args = "push " + packageFileName +
                        " -Source " + server.Uri + "nuget";
                    var r1 = CommandRunner.Run(
                        nugetexe,
                        packageDirectory,
                        args,
                        waitForExit: true,
                        timeOutInMilliseconds: 10000,
                        inputAction: (w) =>
                        {
                            // This user/password pair is first sent to
                            // GET /nuget, then PUT /nuget
                            w.WriteLine("a");
                            w.WriteLine("b");

                            // Send another user/password pair to PUT
                            w.WriteLine("c");
                            w.WriteLine("d");

                            // Now send the right user/password to PUT
                            w.WriteLine("testuser");
                            w.WriteLine("testpassword");
                        },
                        environmentVariables: new Dictionary<string, string>
                        {
                            { "FORCE_NUGET_EXE_INTERACTIVE", "true" }
                        });
                    server.Stop();

                    // Assert
                    Assert.True(0 == r1.Item1, r1.Item2 + " " + r1.Item3);

                    // Because the credential service caches the answer and attempts
                    // to use it for token refresh the first request happens twice
                    // from a server prespective.
                    Assert.Equal(3, credentialForPutRequest.Count);
                    Assert.Equal("a:b", credentialForPutRequest[0]);
                    Assert.Equal("c:d", credentialForPutRequest[1]);
                    Assert.Equal("testuser:testpassword", credentialForPutRequest[2]);
                }
            }
        }

        // Test push command to a server using basic authentication, with -DisableBuffering option
        [SkipMono]
        public void PushCommand_PushToServerBasicAuthDisableBuffering()
        {
            var nugetexe = Util.GetNuGetExePath();

            List<string> credentialForGetRequest = new List<string>();
            List<string> credentialForPutRequest = new List<string>();

            using (var packageDirectory = TestDirectory.Create())
            {
                // Arrange
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                string outputFileName = Path.Combine(packageDirectory, "t1.nupkg");

                using (var server = new MockServer())
                {
                    server.Listener.AuthenticationSchemes = AuthenticationSchemes.Basic;
                    server.Put.Add("/nuget", r => new Action<HttpListenerResponse>(res =>
                    {
                        var h = r.Headers["Authorization"];
                        var credential = System.Text.Encoding.Default.GetString(Convert.FromBase64String(h.Substring(6)));
                        credentialForPutRequest.Add(credential);

                        if (credential.Equals("testuser:testpassword", StringComparison.OrdinalIgnoreCase))
                        {
                            res.StatusCode = (int)HttpStatusCode.OK;
                        }
                        else
                        {
                            res.AddHeader("WWW-Authenticate", "Basic ");
                            res.StatusCode = (int)HttpStatusCode.Unauthorized;
                        }
                    }));
                    server.Start();

                    // Act
                    var args = "push " + packageFileName +
                        " -Source " + server.Uri + "nuget -DisableBuffering";
                    var r1 = CommandRunner.Run(
                        nugetexe,
                        packageDirectory,
                        args,
                        waitForExit: true,
                        timeOutInMilliseconds: 10000,
                        inputAction: (w) =>
                        {
                            // This user/password pair is first sent to
                            // GET /nuget, then PUT /nuget
                            w.WriteLine("a");
                            w.WriteLine("b");

                            // Send another user/password pair to PUT
                            w.WriteLine("c");
                            w.WriteLine("d");

                            // Now send the right user/password to PUT
                            w.WriteLine("testuser");
                            w.WriteLine("testpassword");
                        },
                        environmentVariables: new Dictionary<string, string>
                        {
                            { "FORCE_NUGET_EXE_INTERACTIVE", "true" }
                        });
                    server.Stop();

                    // Assert
                    Assert.Equal(0, r1.Item1);

                    // Because the credential service caches the answer and attempts
                    // to use it for token refresh the first request happens twice
                    // from a server prespective.
                    Assert.Equal(3, credentialForPutRequest.Count);
                    Assert.Equal("a:b", credentialForPutRequest[0]);
                    Assert.Equal("c:d", credentialForPutRequest[1]);
                    Assert.Equal("testuser:testpassword", credentialForPutRequest[2]);
                }
            }
        }

        // Test push command to a server using IntegratedWindowsAuthentication.
        [WindowsNTFact]
        public void PushCommand_PushToServerIntegratedWindowsAuthentication()
        {
            var nugetexe = Util.GetNuGetExePath();

            IPrincipal putUser = null;

            using (var packageDirectory = TestDirectory.Create())
            {
                // Arrange
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                string outputFileName = Path.Combine(packageDirectory, "t1.nupkg");

                using (var server = new MockServer())
                {
                    server.Listener.AuthenticationSchemes = AuthenticationSchemes.IntegratedWindowsAuthentication;
                    server.Put.Add("/nuget", r => new Action<HttpListenerResponse, IPrincipal>((res, user) =>
                    {
                        putUser = user;
                        res.StatusCode = (int)HttpStatusCode.OK;
                    }));
                    server.Start();

                    // Act
                    var args = "push " + packageFileName +
                        " -Source " + server.Uri + "nuget";
                    var r1 = CommandRunner.Run(
                        nugetexe,
                        packageDirectory,
                        args,
                        waitForExit: true,
                        timeOutInMilliseconds: 10000);
                    server.Stop();

                    // Assert
                    Assert.Equal(0, r1.Item1);

                    var currentUser = WindowsIdentity.GetCurrent();
                    Assert.Equal("NTLM", putUser.Identity.AuthenticationType);
                    Assert.Equal(currentUser.Name, putUser.Identity.Name);
                }
            }
        }

        // Test push command to a server using IntegratedWindowsAuthentication with -DisableBuffering option
        [WindowsNTFact]
        public void PushCommand_PushToServerIntegratedWindowsAuthenticationDisableBuffering()
        {
            var nugetexe = Util.GetNuGetExePath();

            IPrincipal putUser = null;

            using (var packageDirectory = TestDirectory.Create())
            {
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                string outputFileName = Path.Combine(packageDirectory, "t1.nupkg");

                using (var server = new MockServer())
                {
                    server.Listener.AuthenticationSchemes = AuthenticationSchemes.IntegratedWindowsAuthentication;
                    server.Put.Add("/nuget", r => new Action<HttpListenerResponse, IPrincipal>((res, user) =>
                    {
                        putUser = user;
                        res.StatusCode = (int)HttpStatusCode.OK;
                    }));
                    server.Start();

                    // Act
                    var args = "push " + packageFileName +
                        " -Source " + server.Uri + "nuget -DisableBuffering";
                    var r1 = CommandRunner.Run(
                        nugetexe,
                        packageDirectory,
                        args,
                        waitForExit: true,
                        timeOutInMilliseconds: 10000);
                    server.Stop();

                    // Assert
                    if (EnvironmentUtility.IsNet45Installed)
                    {
                        Assert.Equal(0, r1.Item1);

                        var currentUser = WindowsIdentity.GetCurrent();
                        Assert.Equal("NTLM", putUser.Identity.AuthenticationType);
                        Assert.Equal(currentUser.Name, putUser.Identity.Name);
                    }
                    else
                    {
                        // On .net 4.0, the process will get killed since integrated windows
                        // authentication won't work when buffering is disabled.
                        Assert.Equal(1, r1.Item1);
                        Assert.Contains("Failed to process request. 'Unauthorized'", r1.Item3);
                        Assert.Contains("This request requires buffering data to succeed.", r1.Item3);
                    }
                }
            }
        }

        // Test push command to a server using Plugin credential provider
        [SkipMono]
        public void PushCommand_PushToServer_GetCredentialFromPlugin()
        {
            var nugetexe = Util.GetNuGetExePath();

            var pluginDirectory = Util.GetTestablePluginDirectory();
            
            

            using (var packageDirectory = TestDirectory.Create())
            {
                // Arrange
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                string outputFileName = Path.Combine(packageDirectory, "t1.nupkg");

                using (var server = new MockServer())
                {
                    var credentialForPutRequest = new List<string>();
                    server.Listener.AuthenticationSchemes = AuthenticationSchemes.Basic;
                    server.Put.Add("/nuget", r => new Action<HttpListenerResponse>(res =>
                    {
                        var h = r.Headers["Authorization"];
                        var credential = Encoding.Default.GetString(Convert.FromBase64String(h.Substring(6)));
                        credentialForPutRequest.Insert(0, credential);

                        if (credential.Equals("testuser:testpassword", StringComparison.OrdinalIgnoreCase))
                        {
                            res.StatusCode = (int)HttpStatusCode.OK;
                        }
                        else
                        {
                            res.AddHeader("WWW-Authenticate", "Basic ");
                            res.StatusCode = (int)HttpStatusCode.Unauthorized;
                        }
                    }));
                    server.Start();

                    // Act
                    var args = $"push {packageFileName} -Source {server.Uri}nuget";
                    var r1 = CommandRunner.Run(
                        nugetexe,
                        packageDirectory,
                        args,
                        waitForExit: true,
                        timeOutInMilliseconds: 10000,
                        inputAction: (w) =>
                        {
                        },
                        environmentVariables: new Dictionary<string, string>
                        {
                            { "FORCE_NUGET_EXE_INTERACTIVE", "true" },
                            { ExtensionLocator.CredentialProvidersEnvar, pluginDirectory },
                            { TestCredentialProvider.ResponseUserName, "testuser" },
                            { TestCredentialProvider.ResponsePassword, "testpassword" },
                            { TestCredentialProvider.ResponseExitCode, TestCredentialProvider.SuccessCode }
                        });
                    server.Stop();

                    // Assert
                    Assert.True(0 == r1.Item1, r1.Item2 + " " + r1.Item3);
                    Assert.NotEqual(0, credentialForPutRequest.Count);
                    Assert.Equal("testuser:testpassword", credentialForPutRequest[0]);
                }
            }
        }

        // Test push command to a server, plugin provider does not provide credentials
        // so fallback to console provider
        [SkipMono]
        public void PushCommand_PushToServer_WhenPluginReturnsNoCredentials_FallBackToConsoleProvider()
        {
            var nugetexe = Util.GetNuGetExePath();
            var pluginDirectory = Util.GetTestablePluginDirectory();
                        

            using (var packageDirectory = TestDirectory.Create())
            {
                // Arrange
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                string outputFileName = Path.Combine(packageDirectory, "t1.nupkg");

                using (var server = new MockServer())
                {
                    var credentialForPutRequest = new List<string>();
                    server.Listener.AuthenticationSchemes = AuthenticationSchemes.Basic;
                    server.Put.Add("/nuget", r => new Action<HttpListenerResponse>(res =>
                    {
                        var h = r.Headers["Authorization"];
                        var credential = Encoding.Default.GetString(Convert.FromBase64String(h.Substring(6)));
                        credentialForPutRequest.Insert(0,credential);
                        if (credential.Equals("testuser:testpassword", StringComparison.OrdinalIgnoreCase))
                        {
                            res.StatusCode = (int)HttpStatusCode.OK;
                        }
                        else
                        {
                            res.AddHeader("WWW-Authenticate", "Basic ");
                            res.StatusCode = (int)HttpStatusCode.Unauthorized;
                        }
                    }));
                    server.Start();

                    // Act
                    var args = $"push {packageFileName} -Source {server.Uri}nuget";
                    var r1 = CommandRunner.Run(
                        nugetexe,
                        packageDirectory,
                        args,
                        waitForExit: true,
                        timeOutInMilliseconds: 10000,
                        inputAction: (w) =>
                        {
                            w.WriteLine("testuser");
                            w.WriteLine("testpassword");
                        },
                        environmentVariables: new Dictionary<string, string>
                        {
                            { "FORCE_NUGET_EXE_INTERACTIVE", "true" },
                            { ExtensionLocator.CredentialProvidersEnvar, pluginDirectory },
                            { TestCredentialProvider.ResponseUserName, string.Empty },
                            { TestCredentialProvider.ResponsePassword, string.Empty },
                            { TestCredentialProvider.ResponseExitCode, TestCredentialProvider.ProviderNotApplicableCode }
                        });
                    server.Stop();

                    // Assert
                    Assert.True(0 == r1.Item1, r1.Item2 + " " + r1.Item3);
                    Assert.NotEqual(0, credentialForPutRequest.Count);
                    Assert.Equal("testuser:testpassword", credentialForPutRequest[0]);
                }
            }
        }

        // Test Plugin credential provider can have large std output without hanging.
        [SkipMono]
        public void PushCommand_PushToServer_DoesNotDeadLockWhenSTDOutLarge()
        {
            var nugetexe = Util.GetNuGetExePath();
            var pluginDirectory = Util.GetTestablePluginDirectory();

            List<string> credentialForGetRequest = new List<string>();
            List<string> credentialForPutRequest = new List<string>();

            using (var packageDirectory = TestDirectory.Create())
            {
                // Arrange
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                string outputFileName = Path.Combine(packageDirectory, "t1.nupkg");

                using (var server = new MockServer())
                {
                    server.Listener.AuthenticationSchemes = AuthenticationSchemes.Basic;
                    server.Put.Add("/nuget", r => new Action<HttpListenerResponse>(res =>
                    {
                        var h = r.Headers["Authorization"];
                        var credential = Encoding.Default.GetString(Convert.FromBase64String(h.Substring(6)));
                        credentialForPutRequest.Add(credential);

                        if (credential.StartsWith("testuser:", StringComparison.OrdinalIgnoreCase))
                        {
                            res.StatusCode = (int)HttpStatusCode.OK;
                        }
                        else
                        {
                            res.AddHeader("WWW-Authenticate", "Basic ");
                            res.StatusCode = (int)HttpStatusCode.Unauthorized;
                        }
                    }));
                    server.Start();
                    var longPassword = new string('a', 10000);

                    // Act
                    var args = $"push {packageFileName} -Source {server.Uri}nuget";
                    var r1 = CommandRunner.Run(
                        nugetexe,
                        packageDirectory,
                        args,
                        waitForExit: true,
                        timeOutInMilliseconds: 10000,
                        inputAction: (w) =>
                        {
                        },
                        environmentVariables: new Dictionary<string, string>
                        {
                            { "FORCE_NUGET_EXE_INTERACTIVE", "true" },
                            { ExtensionLocator.CredentialProvidersEnvar, pluginDirectory },
                            { TestCredentialProvider.ResponseUserName, "testuser" },
                            { TestCredentialProvider.ResponsePassword, longPassword }
                        });
                    server.Stop();

                    // Assert
                    Assert.Equal(0, r1.Item1);

                    Assert.Equal(1, credentialForPutRequest.Count);
                }
            }
        }

        // Test push command to a server, plugin provider returns abort
        [Fact]
        public void PushCommand_PushToServer_WhenPluginReturnsAbort_ThrowsAndDoesNotFallBackToConsoleProvider()
        {
            var nugetexe = Util.GetNuGetExePath();
            var pluginDirectory = Util.GetTestablePluginDirectory();
            var pluginPath = Util.GetTestablePluginPath();

            List<string> credentialForGetRequest = new List<string>();
            List<string> credentialForPutRequest = new List<string>();

            using (var packageDirectory = TestDirectory.Create())
            {
                // Arrange
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                string outputFileName = Path.Combine(packageDirectory, "t1.nupkg");

                using (var server = new MockServer())
                {
                    server.Listener.AuthenticationSchemes = AuthenticationSchemes.Basic;
                    server.Get.Add("/nuget", r =>
                    {
                        var h = r.Headers["Authorization"];
                        var credential = Encoding.Default.GetString(Convert.FromBase64String(h.Substring(6)));
                        credentialForGetRequest.Add(credential);
                        return HttpStatusCode.OK;
                    });
                    server.Put.Add("/nuget", r =>
                    {
                        var h = r.Headers["Authorization"];
                        var credential = Encoding.Default.GetString(Convert.FromBase64String(h.Substring(6)));
                        credentialForPutRequest.Add(credential);
                        return HttpStatusCode.OK;
                    });
                    server.Start();

                    // Act
                    var args = $"push {packageFileName} -Source {server.Uri}nuget";
                    var r1 = CommandRunner.Run(
                        nugetexe,
                        packageDirectory,
                        args,
                        waitForExit: true,
                        timeOutInMilliseconds: 10000,
                        inputAction: (w) =>
                        {
                            w.WriteLine("testuser");
                            w.WriteLine("testpassword");
                        },
                        environmentVariables: new Dictionary<string, string>
                        {
                            { "FORCE_NUGET_EXE_INTERACTIVE", "true" },
                            { ExtensionLocator.CredentialProvidersEnvar, pluginDirectory },
                            { TestCredentialProvider.ResponseMessage, "Testing abort." },
                            { TestCredentialProvider.ResponseExitCode, TestCredentialProvider.FailCode }
                        });
                    server.Stop();

                    // Assert
                    Assert.Equal(1, r1.Item1);
                    Assert.Contains("401 (Unauthorized)", r1.Item2 + " " + r1.Item3);
                    Assert.Contains($"Credential plugin {pluginPath} handles this request, but is unable to provide credentials. Testing abort.", r1.Item2 + " " + r1.Item3);

                    // No requests hit server, since abort during credential acquisition
                    // and no fallback to console provider
                    Assert.Equal(0, credentialForGetRequest.Count);
                    Assert.Equal(0, credentialForPutRequest.Count);
                }
            }
        }

        // Test push command to a server, plugin provider returns abort
        [Fact]
        public void PushCommand_PushToServer_WhenPluginTimesOut_ThrowsAndDoesNotFallBackToConsoleProvider()
        {
            var nugetexe = Util.GetNuGetExePath();

            var pluginDirectory = Util.GetTestablePluginDirectory();
            var pluginPath = Util.GetTestablePluginPath();

            List<string> credentialForGetRequest = new List<string>();
            List<string> credentialForPutRequest = new List<string>();

            using (var packageDirectory = TestDirectory.Create())
            {
                // Arrange
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                string outputFileName = Path.Combine(packageDirectory, "t1.nupkg");

                using (var server = new MockServer())
                {
                    server.Listener.AuthenticationSchemes = AuthenticationSchemes.Basic;
                    server.Get.Add("/nuget", r =>
                    {
                        var h = r.Headers["Authorization"];
                        var credential = Encoding.Default.GetString(Convert.FromBase64String(h.Substring(6)));
                        credentialForGetRequest.Add(credential);
                        return HttpStatusCode.OK;
                    });
                    server.Put.Add("/nuget", r =>
                    {
                        var h = r.Headers["Authorization"];
                        var credential = Encoding.Default.GetString(Convert.FromBase64String(h.Substring(6)));
                        credentialForPutRequest.Add(credential);
                        return HttpStatusCode.OK;
                    });
                    server.Start();

                    // Act
                    var args = $"push {packageFileName} -Source {server.Uri}nuget";
                    var r1 = CommandRunner.Run(
                        nugetexe,
                        packageDirectory,
                        args,
                        waitForExit: true,
                        timeOutInMilliseconds: 10000,
                        inputAction: (w) =>
                        {
                            w.WriteLine("testuser");
                            w.WriteLine("testpassword");
                        },
                        environmentVariables: new Dictionary<string, string>
                        {
                            { "FORCE_NUGET_EXE_INTERACTIVE", "true" },
                            { ExtensionLocator.CredentialProvidersEnvar, pluginDirectory },
                            { TestCredentialProvider.ResponseDelaySeconds, "10" },
                            { "NUGET_CREDENTIAL_PROVIDER_TIMEOUT_SECONDS", "5" }
                        });
                    server.Stop();

                    var output = r1.Item2 + " " + r1.Item3;

                    // Assert
                    Assert.True(1 == r1.Item1, output);
                    Assert.Contains("401 (Unauthorized)", output);
                    Assert.Contains($"Credential plugin {pluginPath} timed out", output);
                    // ensure the process was killed
                    Assert.Equal(0, System.Diagnostics.Process.GetProcessesByName(Path.GetFileNameWithoutExtension(pluginPath)).Length);
                    // No requests hit server, since abort during credential acquisition
                    // and no fallback to console provider
                    Assert.Equal(0, credentialForGetRequest.Count);
                    Assert.Equal(0, credentialForPutRequest.Count);
                }
            }
        }

        [Fact]
        public void PushCommand_PushToServerV3()
        {
            Util.ClearWebCache();
            var nugetexe = Util.GetNuGetExePath();

            using (var packagesDirectory = TestDirectory.Create())
            {
                // Arrange
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packagesDirectory);
                string outputFileName = Path.Combine(packagesDirectory, "t1.nupkg");

                // Server setup
                var indexJson = Util.CreateIndexJson();

                using (var serverV3 = new MockServer())
                {
                    serverV3.Get.Add("/", r =>
                    {
                        var path = serverV3.GetRequestUrlAbsolutePath(r);

                        if (path == "/index.json")
                        {
                            return new Action<HttpListenerResponse>(response =>
                            {
                                response.StatusCode = 200;
                                response.ContentType = "text/javascript";
                                MockServer.SetResponseContent(response, indexJson.ToString());
                            });
                        }

                        throw new Exception("This test needs to be updated to support: " + path);
                    });

                    using (var serverV2 = new MockServer())
                    {
                        Util.AddFlatContainerResource(indexJson, serverV3);
                        Util.AddPublishResource(indexJson, serverV2);

                        serverV2.Get.Add("/push", r => "OK");
                        serverV2.Put.Add("/push", r =>
                        {
                            byte[] buffer = MockServer.GetPushedPackage(r);
                            using (var of = new FileStream(outputFileName, FileMode.Create))
                            {
                                of.Write(buffer, 0, buffer.Length);
                            }

                            return HttpStatusCode.Created;
                        });

                        serverV3.Start();
                        serverV2.Start();

                        // Act
                        string[] args = new string[]
                        {
                            "push",
                            packageFileName,
                            "-Source",
                            serverV3.Uri + "index.json"
                        };

                        var result = CommandRunner.Run(
                                        nugetexe,
                                        Directory.GetCurrentDirectory(),
                                        string.Join(" ", args),
                                        true);
                        serverV2.Stop();
                        serverV3.Stop();

                        // Assert
                        Assert.True(0 == result.Item1, result.Item2 + " " + result.Item3);
                        var output = result.Item2;
                        Assert.Contains("Your package was pushed.", output);
                        AssertFileEqual(packageFileName, outputFileName);
                    }
                }
            }
        }

        [Fact]
        public void PushCommand_PushToServerV3_NoPushEndpoint()
        {
            Util.ClearWebCache();
            var nugetexe = Util.GetNuGetExePath();
            using (var packagesDirectory = TestDirectory.Create())

            {
                // Arrange
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packagesDirectory);
                string outputFileName = Path.Combine(packagesDirectory, "t1.nupkg");

                // Server setup
                var indexJson = Util.CreateIndexJson();

                using (var serverV3 = new MockServer())
                {
                    serverV3.Get.Add("/", r =>
                    {
                        var path = serverV3.GetRequestUrlAbsolutePath(r);

                        if (path == "/index.json")
                        {
                            return new Action<HttpListenerResponse>(response =>
                            {
                                response.StatusCode = 200;
                                response.ContentType = "text/javascript";
                                MockServer.SetResponseContent(response, indexJson.ToString());
                            });
                        }

                        throw new Exception("This test needs to be updated to support: " + path);
                    });

                    serverV3.Start();

                    // Act
                    var result = CommandRunner.Run(
                                    nugetexe,
                                    Directory.GetCurrentDirectory(),
                                    $"push {packageFileName} -Source {serverV3.Uri}index.json",
                                    true);

                    serverV3.Stop();

                    // Assert
                    Assert.True(1 == result.Item1, $"{result.Item2} {result.Item3}");

                    var expectedOutput =
                        string.Format(
                      "ERROR: This version of nuget.exe does not support updating packages to package source '{0}'.",
                      serverV3.Uri + "index.json");

                    // Verify that the output contains the expected output
                    Assert.True(result.Item3.Contains(expectedOutput));
                }
            }
        }

        [Fact]
        public void PushCommand_PushToServerV3_Unavailable()
        {
            Util.ClearWebCache();
            var nugetexe = Util.GetNuGetExePath();

            using (var packagesDirectory = TestDirectory.Create())
            {
                // Arrange
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packagesDirectory);
                string outputFileName = Path.Combine(packagesDirectory, "t1.nupkg");

                // Server setup
                var indexJson = Util.CreateIndexJson();

                using (var serverV3 = new MockServer())
                {
                    serverV3.Get.Add("/", r =>
                    {
                        var path = serverV3.GetRequestUrlAbsolutePath(r);

                        if (path == "/index.json")
                        {
                            return new Action<HttpListenerResponse>(response =>
                            {
                                response.StatusCode = 404;
                                response.ContentType = "text/javascript";
                                MockServer.SetResponseContent(response, response.StatusCode.ToString());
                            });
                        }

                        throw new Exception("This test needs to be updated to support: " + path);
                    });

                    serverV3.Start();

                    // Act
                    string[] args = new string[]
                    {
                            "push",
                            packageFileName,
                            "-Source",
                            serverV3.Uri + "index.json"
                    };

                    var result = CommandRunner.Run(
                                    nugetexe,
                                    Directory.GetCurrentDirectory(),
                                    string.Join(" ", args),
                                    true);

                    serverV3.Stop();

                    // Assert
                    Assert.True(result.Item1 != 0, result.Item2 + " " + result.Item3);

                    Assert.True(
                        result.Item3.Contains("404 (Not Found)"),
                        "Expected error message not found in " + result.Item3
                        );
                }
            }
        }

        [Fact]
        public void PushCommand_PushToServer_ApiKeyAsThirdArgument()
        {
            // Arrange
            var testApiKey = Guid.NewGuid().ToString();
            Util.ClearWebCache();

            using (var packageDirectory = TestDirectory.Create())
            {
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                string outputFileName = Path.Combine(packageDirectory, "t1.nupkg");

                using (var server = new MockServer())
                {
                    server.Put.Add("/nuget", r =>
                    {
                        var h = r.Headers[ApiKeyHeader];
                        if (!string.Equals(h, testApiKey, StringComparison.OrdinalIgnoreCase))
                        {
                            return HttpStatusCode.Unauthorized;
                        }

                        MockServer.SavePushedPackage(r, outputFileName);

                        return HttpStatusCode.Created;
                    });

                    server.Start();

                    // Act
                    var result = CommandRunner.Run(
                        NuGetExePath,
                        Directory.GetCurrentDirectory(),
                        $"push {packageFileName} {testApiKey} -Source {server.Uri}nuget -NonInteractive",
                        waitForExit: true);

                    server.Stop();

                    // Assert
                    Assert.True(0 == result.Item1, $"{result.Item2} {result.Item3}");
                    Assert.Contains("Your package was pushed.", result.Item2);
                    AssertFileEqual(packageFileName, outputFileName);
                }
            }
        }

        [Fact]
        public void PushCommand_PushToServer_ApiKeyAsNamedArgument()
        {
            // Arrange
            var testApiKey = Guid.NewGuid().ToString();
            Util.ClearWebCache();

            using (var packagesDirectory = TestDirectory.Create())
            {
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packagesDirectory);
                var outputFileName = Path.Combine(packagesDirectory, "t1.nupkg");

                using (var serverV3 = new MockServer())
                {
                    // Server setup
                    var indexJson = Util.CreateIndexJson();

                    Util.AddFlatContainerResource(indexJson, serverV3);
                    Util.AddPublishResource(indexJson, serverV3);

                    serverV3.Get.Add("/index.json", r =>
                    {
                        return new Action<HttpListenerResponse>(response =>
                        {
                            response.StatusCode = 200;
                            response.ContentType = "text/javascript";
                            MockServer.SetResponseContent(response, indexJson.ToString());
                        });
                    });

                    serverV3.Get.Add("/push", r => "OK");
                    serverV3.Put.Add("/push", r =>
                    {
                        var h = r.Headers[ApiKeyHeader];
                        if (!string.Equals(h, testApiKey, StringComparison.OrdinalIgnoreCase))
                        {
                            return HttpStatusCode.Unauthorized;
                        }

                        MockServer.SavePushedPackage(r, outputFileName);

                        return HttpStatusCode.Created;
                    });

                    serverV3.Start();

                    // Act
                    var args = new[]
                    {
                        "push",
                        packageFileName,
                        "should-be-ignored", // The named argument is preferred over the positional argument.
                        "-Source",
                        serverV3.Uri + "index.json",
                        "-ApiKey",
                        testApiKey,
                        "-NonInteractive"
                    };

                    var result = CommandRunner.Run(
                        NuGetExePath,
                        Directory.GetCurrentDirectory(),
                        string.Join(" ", args),
                        waitForExit: true);

                    serverV3.Stop();

                    // Assert
                    Assert.True(0 == result.Item1, $"{result.Item2} {result.Item3}");
                    Assert.Contains("Your package was pushed.", result.Item2);
                    AssertFileEqual(packageFileName, outputFileName);
                }
            }
        }

        [Theory]
        [InlineData("{0}index.json")] // package source url
        [InlineData("{0}push")] // push package endpoint
        public void PushCommand_PushToServerV3_ApiKeyFromConfig(string configKeyFormatString)
        {
            var testApiKey = Guid.NewGuid().ToString();
            Util.ClearWebCache();

            using (var randomTestFolder = TestDirectory.Create())
            {
                // Arrange
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", randomTestFolder);
                string outputFileName = Path.Combine(randomTestFolder, "t1.nupkg");

                using (var serverV3 = new MockServer())
                {
                    // Server setup
                    var indexJson = Util.CreateIndexJson();

                    Util.AddFlatContainerResource(indexJson, serverV3);
                    Util.AddPublishResource(indexJson, serverV3);

                    serverV3.Get.Add("/index.json", r =>
                    {
                        return new Action<HttpListenerResponse>(response =>
                        {
                            response.StatusCode = 200;
                            response.ContentType = "text/javascript";
                            MockServer.SetResponseContent(response, indexJson.ToString());
                        });
                    });

                    serverV3.Get.Add("/push", r => "OK");
                    serverV3.Put.Add("/push", r =>
                    {
                        var h = r.Headers[ApiKeyHeader];
                        if (!string.Equals(h, testApiKey, StringComparison.OrdinalIgnoreCase))
                        {
                            return HttpStatusCode.Unauthorized;
                        }

                        MockServer.SavePushedPackage(r, outputFileName);

                        return HttpStatusCode.Created;
                    });

                    serverV3.Start();

                    var configKey = string.Format(configKeyFormatString, serverV3.Uri);

                    var config = $@"<?xml version='1.0' encoding='utf-8'?>
<configuration>
    <packageSources>
        <add key='nuget.org' value='{serverV3.Uri}index.json' protocolVersion='3' />
    </packageSources>
    <apikeys>
        <add key='{configKey}' value='{Configuration.EncryptionUtility.EncryptString(testApiKey)}' />
    </apikeys>
</configuration>";

                    var configFileName = Path.Combine(randomTestFolder, "nuget.config");
                    File.WriteAllText(configFileName, config);

                    // Act
                    var args = new[]
                    {
                        "push",
                        packageFileName,
                        "-Source",
                        "nuget.org",
                        "-ConfigFile",
                        configFileName,
                        "-NonInteractive"
                    };

                    var result = CommandRunner.Run(
                        NuGetExePath,
                        Directory.GetCurrentDirectory(),
                        string.Join(" ", args),
                        waitForExit: true);

                    serverV3.Stop();

                    // Assert
                    Assert.True(0 == result.Item1, $"{result.Item2} {result.Item3}");
                    Assert.Contains("Your package was pushed.", result.Item2);
                    AssertFileEqual(packageFileName, outputFileName);
                }
            }
        }

        [Fact]
        public void PushCommand_FailWhenNoSourceSpecified()
        {
            Util.ClearWebCache();
            var nugetexe = Util.GetNuGetExePath();
            using (var randomDirectory = TestDirectory.Create())

            {
                // Arrange
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", randomDirectory);
                string outputFileName = Path.Combine(randomDirectory, "t1.nupkg");

                // Act
                string[] args = new string[]
                {
                        "push",
                        packageFileName,
                        "-ApiKey",
                        "blah-blah"
                };

                var result = CommandRunner.Run(
                                nugetexe,
                                Directory.GetCurrentDirectory(),
                                string.Join(" ", args),
                                true);

                // Assert
                Assert.True(1 == result.Item1, result.Item2 + " " + result.Item3);
                Assert.Contains("Source parameter was not specified", result.Item3);
            }
        }

        [Fact]
        public void PushCommand_APIV2Package_Endpoint()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var packageDirectory = TestDirectory.Create())
            {
                // Arrange
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                string outputFileName = Path.Combine(packageDirectory, "t1.nupkg");

                using (var server = new MockServer())
                {
                    server.Get.Add("/", r =>
                    {
                        var path = server.GetRequestUrlAbsolutePath(r);

                        if (path.Equals("/", StringComparison.OrdinalIgnoreCase))
                        {
                            return HttpStatusCode.OK;
                        }

                        if (path.Equals("/api/v2", StringComparison.OrdinalIgnoreCase)
                        || path.Equals("/api/v2/", StringComparison.OrdinalIgnoreCase))
                        {
                            return HttpStatusCode.OK;
                        }

                        if (path.Equals("/api/v2/Package", StringComparison.OrdinalIgnoreCase)
                        || path.Equals("/api/v2/Package/", StringComparison.OrdinalIgnoreCase))
                        {
                            return HttpStatusCode.NotFound;
                        }

                        throw new Exception("This test needs to be updated to support GET for: " + path);
                    });

                    server.Put.Add("/", r =>
                    {
                        var path = server.GetRequestUrlAbsolutePath(r);

                        if (path.Equals("/api/v2/Package", StringComparison.OrdinalIgnoreCase)
                        || path.Equals("/api/v2/Package/", StringComparison.OrdinalIgnoreCase))
                        {
                            byte[] buffer = MockServer.GetPushedPackage(r);
                            using (var of = new FileStream(outputFileName, FileMode.Create))
                            {
                                of.Write(buffer, 0, buffer.Length);
                            }

                            return HttpStatusCode.Created;
                        }

                        throw new Exception("This test needs to be updated to support PUT for: " + path);
                    });
                    server.Start();

                    // Act
                    string[] args = new string[]
                    {
                        "push",
                        packageFileName,
                        "-Source",
                        server.Uri + "api/v2/Package"
                    };

                    var result = CommandRunner.Run(
                                    nugetexe,
                                    Directory.GetCurrentDirectory(),
                                    string.Join(" ", args),
                                    true);
                    server.Stop();

                    // Assert
                    Assert.True(0 == result.Item1, result.Item2 + " " + result.Item3);
                    var output = result.Item2;
                    Assert.Contains("Your package was pushed.", output);
                    AssertFileEqual(packageFileName, outputFileName);
                }
            }
        }

        [Theory]
        [InlineData("invalid")]
        public void PushCommand_InvalidInput_NonSource(string invalidInput)
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var packagesDirectory = TestDirectory.Create())
            {
                // Arrange
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packagesDirectory);

                // Act
                var args = new string[]
                {
                        "push",
                        packageFileName,
                        "-Source",
                        invalidInput
                };

                var result = CommandRunner.Run(
                                nugetexe,
                                Directory.GetCurrentDirectory(),
                                string.Join(" ", args),
                                true);

                // Assert
                Assert.True(
                    result.Item1 != 0,
                    "The run did not fail as desired. Simply got this output:" + result.Item2);

                Assert.True(
                    result.Item3.Contains(
                        string.Format(
                            "The specified source '{0}' is invalid. Please provide a valid source.",
                            invalidInput)),
                    "Expected error message not found in " + result.Item3
                    );
            }
        }

        [Theory]
        [InlineData("https://invalid-2a0358f1-88f2-48c0-b68a-bb150cac00bd.org")]
        [InlineData("https://invalid-2a0358f1-88f2-48c0-b68a-bb150cac00bd.org/api/v2")]
        [InlineData("https://invalid-2a0358f1-88f2-48c0-b68a-bb150cac00bd.org/api/v2/Package")]
        public void PushCommand_InvalidInput_V2HttpSource(string invalidInput)
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var packagesDirectory = TestDirectory.Create())

            {
                // Arrange
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packagesDirectory);

                // Act
                var args = new string[]
                {
                        "push",
                        packageFileName,
                        "-Source",
                        invalidInput
                };

                var result = CommandRunner.Run(
                                nugetexe,
                                Directory.GetCurrentDirectory(),
                                string.Join(" ", args),
                                true);

                // Assert
                Assert.True(
                    result.Item1 != 0,
                    "The run did not fail as desired. Simply got this output:" + result.Item2);

                if (RuntimeEnvironmentHelper.IsMono)
                {
                    Assert.True(
                   result.Item3.Contains(
                       "NameResolutionFailure"),
                   "Expected error message not found in " + result.Item3
                   );
                }
                else
                {
                    Assert.True(
                        result.Item3.Contains(
                            "The remote name could not be resolved: 'invalid-2a0358f1-88f2-48c0-b68a-bb150cac00bd.org'"),
                        "Expected error message not found in " + result.Item3
                        );
                }
            }
        }

        [SkipMonoTheory]
        [InlineData("https://nuget.org/api/blah")]
        public void PushCommand_InvalidInput_V2_NonExistent(string invalidInput)
        {
            var nugetexe = Util.GetNuGetExePath();
            using (var packagesDirectory = TestDirectory.Create())
            {
                // Arrange
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packagesDirectory);

                // Act
                var args = new string[]
                {
                        "push",
                        packageFileName,
                        "-Source",
                        invalidInput
                };

                var result = CommandRunner.Run(
                                nugetexe,
                                Directory.GetCurrentDirectory(),
                                string.Join(" ", args),
                                true);

                // Assert
                Assert.True(
                    result.Item1 != 0,
                    "The run did not fail as desired. Simply got this output:" + result.Item2);

                //TODO: review with nuget team, that this new error is good
                Assert.True(
                    result.Item3.Contains(
                        "Response status code does not indicate success: 404 (Not Found)"),
                    "Expected error message not found in " + result.Item3
                    );
            }
        }

        [Theory]
        [InlineData("https://invalid-2a0358f1-88f2-48c0-b68a-bb150cac00bd.org/v3/index.json")]
        public void PushCommand_InvalidInput_V3_NonExistent(string invalidInput)
        {
            Util.ClearWebCache();
            var nugetexe = Util.GetNuGetExePath();
            using (var packagesDirectory = TestDirectory.Create())

            {
                // Arrange
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packagesDirectory);

                // Act
                var args = new string[]
                {
                        "push",
                        packageFileName,
                        "-Source",
                        invalidInput
                };

                var result = CommandRunner.Run(
                                nugetexe,
                                Directory.GetCurrentDirectory(),
                                string.Join(" ", args),
                                true);

                // Assert
                Assert.True(
                    result.Item1 != 0,
                    "The run did not fail as desired. Simply got this output:" + result.Item2);

                if (RuntimeEnvironmentHelper.IsMono)
                {
                    Assert.True(
                   result.Item3.Contains(
                       "NameResolutionFailure"),
                   "Expected error message not found in " + result.Item3
                   );
                }
                else
                {
                    Assert.True(
                        result.Item3.Contains("An error occurred while sending the request."),
                        "Expected error message not found in " + result.Item3
                        );
                }
            }
        }

        [Theory]
        [InlineData("https://api.nuget.org/v4/index.json")]
        public void PushCommand_InvalidInput_V3_NotFound(string invalidInput)
        {
            Util.ClearWebCache();
            var nugetexe = Util.GetNuGetExePath();
            using (var packagesDirectory = TestDirectory.Create())

            {
                // Arrange
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packagesDirectory);

                // Act
                var args = new string[]
                {
                        "push",
                        packageFileName,
                        "-Source",
                        invalidInput
                };

                var result = CommandRunner.Run(
                                nugetexe,
                                Directory.GetCurrentDirectory(),
                                string.Join(" ", args),
                                true);

                // Assert
                Assert.True(
                    result.Item1 != 0,
                    "The run did not fail as desired. Simply got this output:" + result.Item2);

                Assert.True(
                    result.Item3.Contains("400 (Bad Request)"),
                    "Expected error message not found in " + result.Item3
                    );
            }
        }

        [Theory]
        [InlineData("push")]
        [InlineData("push a b c")]
        [InlineData("push a b c -Timeout 2")]
        public void PushCommand_Failure_InvalidArguments(string cmd)
        {
            Util.TestCommandInvalidArguments(cmd);
        }

        // Asserts that the contents of two files are equal.
        void AssertFileEqual(string fileName1, string fileName2)
        {
            Assert.Equal(File.ReadAllBytes(fileName1), File.ReadAllBytes(fileName2));
        }

        private class TestCredentialProvider
        {
            public static readonly string ResponseMessage = "NUGET_TESTABLECREDENTIALPROVIDER_RESPONSEABORTMESSAGE";
            public static readonly string ResponseDelaySeconds = "NUGET_TESTABLECREDENTIALPROVIDER_RESPONSEDELAYSECONDS";
            public static readonly string ResponseExitCode = "NUGET_TESTABLECREDENTIALPROVIDER_RESPONSEEXITCODE";
            public static readonly string ResponsePassword = "NUGET_TESTABLECREDENTIALPROVIDER_RESPONSEPASSWORD";
            public static readonly string ResponseShouldThrow = "NUGET_TESTABLECREDENTIALPROVIDER_RESPONSESHOULDTHROW";
            public static readonly string ResponseUserName = "NUGET_TESTABLECREDENTIALPROVIDER_RESPONSEUSERNAME";
            public static readonly string SuccessCode = "0";
            public static readonly string ProviderNotApplicableCode = "1";
            public static readonly string FailCode = "2";

        }

        public static IEnumerable<object[]> ServerWarningData
        {
            get
            {
                return new[]
                {
                    new string[] { null, null },
                    new string[] { "Single server warning message", null},
                    new string[] { "First of two server warning messages", "Second of two server warning messages"}
                };
            }
        }
    }
}
