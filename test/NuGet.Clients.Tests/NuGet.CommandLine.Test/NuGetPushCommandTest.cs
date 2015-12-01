using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Principal;
using System.Text;
using Test.Utility;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class NuGetPushCommandTest
    {
        private string _originalCredentialProvidersEnvar;
        public NuGetPushCommandTest()
        {
            _originalCredentialProvidersEnvar = Environment.GetEnvironmentVariable(ExtensionLocator.CredentialProvidersEnvar);
        }

        // Tests pushing to a source that is a file system directory.
        [Fact]
        public void PushCommand_PushToFileSystemSource()
        {
            var nugetexe = Util.GetNuGetExePath();
            var tempPath = Path.GetTempPath();
            var packageDirectory = Path.Combine(tempPath, Guid.NewGuid().ToString());
            var source = Path.Combine(tempPath, Guid.NewGuid().ToString());

            try
            {
                // Arrange
                Util.CreateDirectory(packageDirectory);
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
            finally
            {
                // Cleanup
                Util.DeleteDirectory(packageDirectory);
                Util.DeleteDirectory(source);
            }
        }

        // Same as PushCommand_PushToFileSystemSource, except that the directory is specified
        // in unix style.
        [Fact]
        public void PushCommand_PushToFileSystemSourceUnixStyle()
        {
            var nugetexe = Util.GetNuGetExePath();
            var tempPath = Path.GetTempPath();
            var packageDirectory = Path.Combine(tempPath, Guid.NewGuid().ToString());
            var source = Path.Combine(tempPath, Guid.NewGuid().ToString());
            source = source.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            try
            {
                // Arrange
                Util.CreateDirectory(packageDirectory);
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
            }
            finally
            {
                // Cleanup
                Util.DeleteDirectory(packageDirectory);
                Util.DeleteDirectory(source);
            }
        }

        // Same as PushCommand_PushToFileSystemSource, except that the directory is specified
        // in UNC format.
        [Fact]
        public void PushCommand_PushToFileSystemSourceUncStyle()
        {
            // UNC only works in Windows. So skip this test if we're running on Unix,
            if (Path.DirectorySeparatorChar == '/')
            {
                return;
            }

            var nugetexe = Util.GetNuGetExePath();
            var tempPath = Path.GetTempPath();
            var packageDirectory = Path.Combine(tempPath, Guid.NewGuid().ToString());
            var source = Path.Combine(tempPath, Guid.NewGuid().ToString());

            try
            {
                // Arrange
                Util.CreateDirectory(packageDirectory);
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                var uncSource = @"\\localhost\" + source.Replace(':', '$');

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
            finally
            {
                // Cleanup
                Util.DeleteDirectory(packageDirectory);
                Util.DeleteDirectory(source);
            }
        }

        // Tests pushing to an http source
        [Fact]
        public void PushCommand_PushToServer()
        {
            var nugetexe = Util.GetNuGetExePath();
            var tempPath = Path.GetTempPath();
            var packageDirectory = Path.Combine(tempPath, Guid.NewGuid().ToString());

            try
            {
                // Arrange
                Util.CreateDirectory(packageDirectory);
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
                    string[] args = new string[] { "push", packageFileName, "-Source", server.Uri + "push" };
                    var result = CommandRunner.Run(
                                    nugetexe,
                                    Directory.GetCurrentDirectory(),
                                    string.Join(" ", args),
                                    true);
                    server.Stop();

                    // Assert
                    Assert.Equal(0, result.Item1);
                    var output = result.Item2;
                    Assert.Contains("Your package was pushed.", output);
                    AssertFileEqual(packageFileName, outputFileName);
                }
            }
            finally
            {
                // Cleanup
                Util.DeleteDirectory(packageDirectory);
            }
        }

        // Tests that push command can follow redirection correctly.
        [Fact]
        public void PushCommand_PushToServerFollowRedirection()
        {
            var nugetexe = Util.GetNuGetExePath();
            var tempPath = Path.GetTempPath();
            var packageDirectory = Path.Combine(tempPath, Guid.NewGuid().ToString());

            try
            {
                // Arrange
                Util.CreateDirectory(packageDirectory);
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
                    Assert.Equal(0, result.Item1);
                    Assert.Contains("Your package was pushed.", output);
                    AssertFileEqual(packageFileName, outputFileName);
                }
            }
            finally
            {
                // Cleanup
                Util.DeleteDirectory(packageDirectory);
            }
        }

        // Tests that push command will terminate even when there is an infinite
        // redirection loop.
        [Fact]
        public void PushCommand_PushToServerWithInfiniteRedirectionLoop()
        {
            var nugetexe = Util.GetNuGetExePath();
            var tempPath = Path.GetTempPath();
            var packageDirectory = Path.Combine(tempPath, Guid.NewGuid().ToString());

            try
            {
                // Arrange
                Util.CreateDirectory(packageDirectory);
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
            finally
            {
                // Cleanup
                Util.DeleteDirectory(packageDirectory);
            }
        }

        // Tests that push command generates error when it detects invalid redirection location.
        [Fact]
        public void PushCommand_PushToServerWithInvalidRedirectionLocation()
        {
            var nugetexe = Util.GetNuGetExePath();
            var tempPath = Path.GetTempPath();
            var packageDirectory = Path.Combine(tempPath, Guid.NewGuid().ToString());

            try
            {
                // Arrange
                Util.CreateDirectory(packageDirectory);
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
            finally
            {
                // Cleanup
                Util.DeleteDirectory(packageDirectory);
            }
        }

        // Regression test for the bug that "nuget.exe push" will retry forever instead of asking for
        // user's password when NuGet.Server uses Windows Authentication.
        [Fact]
        public void PushCommand_PushToServerWontRetryForever()
        {
            var nugetexe = Util.GetNuGetExePath();
            var tempPath = Path.GetTempPath();
            var packageDirectory = Path.Combine(tempPath, Guid.NewGuid().ToString());

            try
            {
                // Arrange
                Util.CreateDirectory(packageDirectory);
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
            finally
            {
                // Cleanup
                Util.DeleteDirectory(packageDirectory);
            }
        }

        // Test push command to a server using basic authentication.
        [Fact]
        public void PushCommand_PushToServerBasicAuth()
        {
            var nugetexe = Util.GetNuGetExePath();
            var tempPath = Path.GetTempPath();
            var packageDirectory = Path.Combine(tempPath, Guid.NewGuid().ToString());

            List<string> credentialForGetRequest = new List<string>();
            List<string> credentialForPutRequest = new List<string>();
            try
            {
                // Arrange
                Util.CreateDirectory(packageDirectory);
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
                    Environment.SetEnvironmentVariable("FORCE_NUGET_EXE_INTERACTIVE", "true");
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
                        });
                    server.Stop();

                    // Assert
                    Assert.Equal(0, r1.Item1);

                    Assert.Equal(1, credentialForGetRequest.Count);
                    Assert.Equal("a:b", credentialForGetRequest[0]);

                    // Because the credential service caches the answer and attempts
                    // to use it for token refresh the first request happens twice
                    // from a server prespective.
                    Assert.Equal(4, credentialForPutRequest.Count);
                    Assert.Equal("a:b", credentialForPutRequest[0]);
                    Assert.Equal("a:b", credentialForPutRequest[1]);
                    Assert.Equal("c:d", credentialForPutRequest[2]);
                    Assert.Equal("testuser:testpassword", credentialForPutRequest[3]);
                }
            }
            finally
            {
                // Cleanup
                Environment.SetEnvironmentVariable("FORCE_NUGET_EXE_INTERACTIVE", String.Empty);
                Util.DeleteDirectory(packageDirectory);
            }
        }

        // Test push command to a server using basic authentication, with -DisableBuffering option
        [Fact]
        public void PushCommand_PushToServerBasicAuthDisableBuffering()
        {
            var nugetexe = Util.GetNuGetExePath();
            var tempPath = Path.GetTempPath();
            var packageDirectory = Path.Combine(tempPath, Guid.NewGuid().ToString());

            List<string> credentialForGetRequest = new List<string>();
            List<string> credentialForPutRequest = new List<string>();
            try
            {
                // Arrange
                Util.CreateDirectory(packageDirectory);
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                string outputFileName = Path.Combine(packageDirectory, "t1.nupkg");

                using (var server = new MockServer())
                {
                    server.Listener.AuthenticationSchemes = AuthenticationSchemes.Basic;
                    server.Get.Add("/nuget", r =>
                    {
                        var h = r.Headers["Authorization"];
                        var credential = System.Text.Encoding.Default.GetString(Convert.FromBase64String(h.Substring(6)));
                        credentialForGetRequest.Add(credential);
                        return HttpStatusCode.OK;
                    });
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
                    Environment.SetEnvironmentVariable("FORCE_NUGET_EXE_INTERACTIVE", "true");
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
                        });
                    server.Stop();

                    // Assert
                    Assert.Equal(0, r1.Item1);

                    Assert.Equal(1, credentialForGetRequest.Count);
                    Assert.Equal("a:b", credentialForGetRequest[0]);

                    // Because the credential service caches the answer and attempts
                    // to use it for token refresh the first request happens twice
                    // from a server prespective.
                    Assert.Equal(4, credentialForPutRequest.Count);
                    Assert.Equal("a:b", credentialForPutRequest[0]);
                    Assert.Equal("a:b", credentialForPutRequest[1]);
                    Assert.Equal("c:d", credentialForPutRequest[2]);
                    Assert.Equal("testuser:testpassword", credentialForPutRequest[3]);
                }
            }
            finally
            {
                // Cleanup
                Environment.SetEnvironmentVariable("FORCE_NUGET_EXE_INTERACTIVE", String.Empty);
                Util.DeleteDirectory(packageDirectory);
            }
        }

        // Test push command to a server using IntegratedWindowsAuthentication.
        [Fact]
        public void PushCommand_PushToServerIntegratedWindowsAuthentication()
        {
            var nugetexe = Util.GetNuGetExePath();
            var tempPath = Path.GetTempPath();
            var packageDirectory = Path.Combine(tempPath, Guid.NewGuid().ToString());

            IPrincipal getUser = null;
            IPrincipal putUser = null;

            try
            {
                // Arrange
                Util.CreateDirectory(packageDirectory);
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                string outputFileName = Path.Combine(packageDirectory, "t1.nupkg");

                using (var server = new MockServer())
                {
                    server.Listener.AuthenticationSchemes = AuthenticationSchemes.IntegratedWindowsAuthentication;
                    server.Get.Add("/nuget", r => new Action<HttpListenerResponse, IPrincipal>((res, user) =>
                    {
                        getUser = user;
                        res.StatusCode = (int)HttpStatusCode.OK;
                    }));
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
                    Assert.Equal("NTLM", getUser.Identity.AuthenticationType);
                    Assert.Equal(currentUser.Name, getUser.Identity.Name);

                    Assert.Equal("NTLM", putUser.Identity.AuthenticationType);
                    Assert.Equal(currentUser.Name, putUser.Identity.Name);
                }
            }
            finally
            {
                // Cleanup
                Util.DeleteDirectory(packageDirectory);
            }
        }

        // Test push command to a server using IntegratedWindowsAuthentication with -DisableBuffering option
        [Fact]
        public void PushCommand_PushToServerIntegratedWindowsAuthenticationDisableBuffering()
        {
            var nugetexe = Util.GetNuGetExePath();
            var tempPath = Path.GetTempPath();
            var packageDirectory = Path.Combine(tempPath, Guid.NewGuid().ToString());

            IPrincipal getUser = null;
            IPrincipal putUser = null;

            try
            {
                // Arrange
                Util.CreateDirectory(packageDirectory);
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                string outputFileName = Path.Combine(packageDirectory, "t1.nupkg");

                using (var server = new MockServer())
                {
                    server.Listener.AuthenticationSchemes = AuthenticationSchemes.IntegratedWindowsAuthentication;
                    server.Get.Add("/nuget", r => new Action<HttpListenerResponse, IPrincipal>((res, user) =>
                    {
                        getUser = user;
                        res.StatusCode = (int)HttpStatusCode.OK;
                    }));
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
                        Assert.Equal("NTLM", getUser.Identity.AuthenticationType);
                        Assert.Equal(currentUser.Name, getUser.Identity.Name);

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
            finally
            {
                // Cleanup
                Util.DeleteDirectory(packageDirectory);
            }
        }

        // Test push command to a server using Plugin credential provider
        [Fact]
        public void PushCommand_PushToServer_GetCredentialFromPlugin()
        {
            var nugetexe = Util.GetNuGetExePath();
            var tempPath = Path.GetTempPath();
            var packageDirectory = Path.Combine(tempPath, Guid.NewGuid().ToString());
            var pluginDirectory = Util.GetTestablePluginDirectory();

            List<string> credentialForGetRequest = new List<string>();
            List<string> credentialForPutRequest = new List<string>();
            try
            {
                // Arrange
                Util.CreateDirectory(packageDirectory);
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
                    server.Put.Add("/nuget", r => new Action<HttpListenerResponse>(res =>
                    {
                        var h = r.Headers["Authorization"];
                        var credential = Encoding.Default.GetString(Convert.FromBase64String(h.Substring(6)));
                        credentialForPutRequest.Add(credential);

                        if (credential.Equals("testuser:testpassword", StringComparison.OrdinalIgnoreCase))
                        {
                            res.StatusCode = (int) HttpStatusCode.OK;
                        }
                        else
                        {
                            res.AddHeader("WWW-Authenticate", "Basic ");
                            res.StatusCode = (int) HttpStatusCode.Unauthorized;
                        }
                    }));
                    server.Start();

                    // Act
                    Environment.SetEnvironmentVariable("FORCE_NUGET_EXE_INTERACTIVE", "true");
                    Environment.SetEnvironmentVariable(ExtensionLocator.CredentialProvidersEnvar, pluginDirectory);
                    Environment.SetEnvironmentVariable(TestCredentialProvider.ResponseUserName, "testuser");
                    Environment.SetEnvironmentVariable(TestCredentialProvider.ResponsePassword, "testpassword");
                    Environment.SetEnvironmentVariable(TestCredentialProvider.ResponseExitCode, TestCredentialProvider.SuccessCode);

                    var args = $"push {packageFileName} -Source {server.Uri}nuget";
                    var r1 = CommandRunner.Run(
                        nugetexe,
                        packageDirectory,
                        args,
                        waitForExit: true,
                        timeOutInMilliseconds: 10000,
                        inputAction: (w) =>
                        {

                        });
                    server.Stop();

                    // Assert
                    Assert.Equal(0, r1.Item1);

                    Assert.Equal(1, credentialForGetRequest.Count);
                    Assert.Equal("testuser:testpassword", credentialForGetRequest[0]);

                    Assert.Equal(1, credentialForPutRequest.Count);
                    Assert.Equal("testuser:testpassword", credentialForPutRequest[0]);
                }
            }
            finally
            {
                // Cleanup
                Environment.SetEnvironmentVariable("FORCE_NUGET_EXE_INTERACTIVE", string.Empty);
                Environment.SetEnvironmentVariable(ExtensionLocator.CredentialProvidersEnvar, _originalCredentialProvidersEnvar);
                Environment.SetEnvironmentVariable(TestCredentialProvider.ResponseUserName, string.Empty);
                Environment.SetEnvironmentVariable(TestCredentialProvider.ResponsePassword, string.Empty);
                Environment.SetEnvironmentVariable(TestCredentialProvider.ResponseExitCode, string.Empty);
                Util.DeleteDirectory(packageDirectory);
            }
        }

        // Test push command to a server, plugin provider does not provide credentials
        // so fallback to console provider
        [Fact]
        public void PushCommand_PushToServer_WhenPluginReturnsNoCredentials_FallBackToConsoleProvider()
        {
            var nugetexe = Util.GetNuGetExePath();
            var tempPath = Path.GetTempPath();
            var packageDirectory = Path.Combine(tempPath, Guid.NewGuid().ToString());
            var pluginDirectory = Util.GetTestablePluginDirectory();

            List<string> credentialForGetRequest = new List<string>();
            List<string> credentialForPutRequest = new List<string>();
            try
            {
                // Arrange
                Util.CreateDirectory(packageDirectory);
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
                    Environment.SetEnvironmentVariable("FORCE_NUGET_EXE_INTERACTIVE", "true");
                    Environment.SetEnvironmentVariable(ExtensionLocator.CredentialProvidersEnvar, pluginDirectory);
                    Environment.SetEnvironmentVariable(TestCredentialProvider.ResponseUserName, string.Empty);
                    Environment.SetEnvironmentVariable(TestCredentialProvider.ResponsePassword, string.Empty);
                    Environment.SetEnvironmentVariable(TestCredentialProvider.ResponseExitCode, TestCredentialProvider.ProviderNotApplicableCode);

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
                        });
                    server.Stop();

                    // Assert
                    Assert.Equal(0, r1.Item1);

                    Assert.Equal(1, credentialForGetRequest.Count);
                    Assert.Equal("testuser:testpassword", credentialForGetRequest[0]);

                    Assert.Equal(1, credentialForPutRequest.Count);
                    Assert.Equal("testuser:testpassword", credentialForPutRequest[0]);
                }
            }
            finally
            {
                // Cleanup
                Environment.SetEnvironmentVariable("FORCE_NUGET_EXE_INTERACTIVE", string.Empty);
                Environment.SetEnvironmentVariable(ExtensionLocator.CredentialProvidersEnvar, _originalCredentialProvidersEnvar);
                Environment.SetEnvironmentVariable(TestCredentialProvider.ResponseExitCode, string.Empty);
                Util.DeleteDirectory(packageDirectory);
            }
        }

        // Test Plugin credential provider can have large std output without hanging.
        [Fact]
        public void PushCommand_PushToServer_DoesNotDeadLockWhenSTDOutLarge()
        {
            var nugetexe = Util.GetNuGetExePath();
            var tempPath = Path.GetTempPath();
            var packageDirectory = Path.Combine(tempPath, Guid.NewGuid().ToString());
            var pluginDirectory = Util.GetTestablePluginDirectory();

            List<string> credentialForGetRequest = new List<string>();
            List<string> credentialForPutRequest = new List<string>();
            try
            {
                // Arrange
                Util.CreateDirectory(packageDirectory);
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
                    server.Put.Add("/nuget", r => new Action<HttpListenerResponse>(res =>
                    {
                        var h = r.Headers["Authorization"];
                        var credential = Encoding.Default.GetString(Convert.FromBase64String(h.Substring(6)));
                        credentialForPutRequest.Add(credential);

                        if (credential.StartsWith("testuser:", StringComparison.OrdinalIgnoreCase))
                        {
                            res.StatusCode = (int) HttpStatusCode.OK;
                        }
                        else
                        {
                            res.AddHeader("WWW-Authenticate", "Basic ");
                            res.StatusCode = (int) HttpStatusCode.Unauthorized;
                        }
                    }));
                    server.Start();

                    Environment.SetEnvironmentVariable("FORCE_NUGET_EXE_INTERACTIVE", "true");
                    Environment.SetEnvironmentVariable(ExtensionLocator.CredentialProvidersEnvar, pluginDirectory);
                    Environment.SetEnvironmentVariable(TestCredentialProvider.ResponseUserName, "testuser");

                    var longPassword = new string('a', 10000);
                    Environment.SetEnvironmentVariable(TestCredentialProvider.ResponsePassword, longPassword);

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
                        });
                    server.Stop();

                    // Assert
                    Assert.Equal(0, r1.Item1);

                    Assert.Equal(1, credentialForGetRequest.Count);
                    Assert.Equal(1, credentialForPutRequest.Count);
                }
            }
            finally
            {
                // Cleanup
                Environment.SetEnvironmentVariable("FORCE_NUGET_EXE_INTERACTIVE", string.Empty);
                Environment.SetEnvironmentVariable(ExtensionLocator.CredentialProvidersEnvar, _originalCredentialProvidersEnvar);
                Environment.SetEnvironmentVariable(TestCredentialProvider.ResponseUserName, string.Empty);
                Util.DeleteDirectory(packageDirectory);
            }
        }

        // Test push command to a server, plugin provider returns abort
        [Fact]
        public void PushCommand_PushToServer_WhenPluginReturnsAbort_ThrowsAndDoesNotFallBackToConsoleProvider()
        {
            var nugetexe = Util.GetNuGetExePath();
            var tempPath = Path.GetTempPath();
            var packageDirectory = Path.Combine(tempPath, Guid.NewGuid().ToString());
            var pluginDirectory = Util.GetTestablePluginDirectory();
            var pluginPath = Util.GetTestablePluginPath();

            List<string> credentialForGetRequest = new List<string>();
            List<string> credentialForPutRequest = new List<string>();
            try
            {
                // Arrange
                Util.CreateDirectory(packageDirectory);
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
                    Environment.SetEnvironmentVariable("FORCE_NUGET_EXE_INTERACTIVE", "true");
                    Environment.SetEnvironmentVariable(ExtensionLocator.CredentialProvidersEnvar, pluginDirectory);
                    Environment.SetEnvironmentVariable(TestCredentialProvider.ResponseMessage, "Testing abort.");
                    Environment.SetEnvironmentVariable(TestCredentialProvider.ResponseExitCode, TestCredentialProvider.FailCode);

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
                        });
                    server.Stop();

                    // Assert
                    Assert.Equal(1, r1.Item1);

                    Assert.Contains($"Credential plugin {pluginPath} handles this request, but is unable to provide credentials. Testing abort.", r1.Item3);

                    // No requests hit server, since abort during credential acquisition
                    // and no fallback to console provider
                    Assert.Equal(0, credentialForGetRequest.Count);
                    Assert.Equal(0, credentialForPutRequest.Count);
                }
            }
            finally
            {
                // Cleanup
                Environment.SetEnvironmentVariable("FORCE_NUGET_EXE_INTERACTIVE", string.Empty);
                Environment.SetEnvironmentVariable(ExtensionLocator.CredentialProvidersEnvar, _originalCredentialProvidersEnvar);
                Environment.SetEnvironmentVariable(TestCredentialProvider.ResponseMessage, string.Empty);
                Environment.SetEnvironmentVariable(TestCredentialProvider.ResponseExitCode, string.Empty);
                Util.DeleteDirectory(packageDirectory);
            }
        }

        // Test push command to a server, plugin provider returns abort
        [Fact]
        public void PushCommand_PushToServer_WhenPluginTimesOut_ThrowsAndDoesNotFallBackToConsoleProvider()
        {
            var nugetexe = Util.GetNuGetExePath();
            var tempPath = Path.GetTempPath();
            var packageDirectory = Path.Combine(tempPath, Guid.NewGuid().ToString());
            var pluginDirectory = Util.GetTestablePluginDirectory();
            var pluginPath = Util.GetTestablePluginPath();

            List<string> credentialForGetRequest = new List<string>();
            List<string> credentialForPutRequest = new List<string>();
            try
            {
                // Arrange
                Util.CreateDirectory(packageDirectory);
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
                    Environment.SetEnvironmentVariable("FORCE_NUGET_EXE_INTERACTIVE", "true");
                    Environment.SetEnvironmentVariable(ExtensionLocator.CredentialProvidersEnvar, pluginDirectory);
                    Environment.SetEnvironmentVariable(TestCredentialProvider.ResponseDelaySeconds, "10");
                    Environment.SetEnvironmentVariable("NUGET_CREDENTIAL_PROVIDER_TIMEOUT_SECONDS", "5");

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
                        });
                    server.Stop();

                    // Assert
                    Assert.Equal(1, r1.Item1);
                    Assert.Contains($"Credential plugin {pluginPath} timed out", r1.Item3);
                    // ensure the process was killed
                    Assert.Equal(0, System.Diagnostics.Process.GetProcessesByName(Path.GetFileNameWithoutExtension(pluginPath)).Length);
                    // No requests hit server, since abort during credential acquisition
                    // and no fallback to console provider
                    Assert.Equal(0, credentialForGetRequest.Count);
                    Assert.Equal(0, credentialForPutRequest.Count);
                }
            }
            finally
            {
                // Cleanup
                Environment.SetEnvironmentVariable("FORCE_NUGET_EXE_INTERACTIVE", string.Empty);
                Environment.SetEnvironmentVariable(ExtensionLocator.CredentialProvidersEnvar, _originalCredentialProvidersEnvar);
                Environment.SetEnvironmentVariable(TestCredentialProvider.ResponseDelaySeconds, string.Empty);
                Environment.SetEnvironmentVariable("NUGET_CREDENTIAL_PROVIDER_TIMEOUT_SECONDS", string.Empty);
                Util.DeleteDirectory(packageDirectory);
            }
        }

        [Fact]
        public void PushCommand_PushToServerV3()
        {
            var nugetexe = Util.GetNuGetExePath();
            var packagesDirectory = TestFilesystemUtility.CreateRandomTestFolder();

            try
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
                        var path = r.Url.AbsolutePath;

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
            finally
            {
                // Cleanup
                TestFilesystemUtility.DeleteRandomTestFolders(packagesDirectory);
            }
        }

        [Fact]
        public void PushCommand_PushToServerV3_NoPushEndpoint()
        {
            var nugetexe = Util.GetNuGetExePath();
            var packagesDirectory = TestFilesystemUtility.CreateRandomTestFolder();

            try
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
                        var path = r.Url.AbsolutePath;

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
                    Assert.True(result.Item1 == 0, result.Item2 + " " + result.Item3);

                    var expectedOutput =
                        string.Format(
                      "WARNING: This version of nuget.exe does not support pushing packages to package source '{0}'.",
                      serverV3.Uri + "index.json");

                    // Verify that the output contains the expected output
                    Assert.True(result.Item2.Contains(expectedOutput));
                }
            }
            finally
            {
                // Cleanup
                TestFilesystemUtility.DeleteRandomTestFolders(packagesDirectory);
            }
        }

        [Fact]
        public void PushCommand_PushToServerV3_Unavailable()
        {
            var nugetexe = Util.GetNuGetExePath();
            var packagesDirectory = TestFilesystemUtility.CreateRandomTestFolder();

            try
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
                        var path = r.Url.AbsolutePath;

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
                        result.Item3.Contains("Response status code does not indicate success: 404 (Not Found)."),
                        "Expected error message not found in " + result.Item3
                        );
                }
            }
            finally
            {
                // Cleanup
                TestFilesystemUtility.DeleteRandomTestFolders(packagesDirectory);
            }
        }

        [Fact]
        public void PushCommand_PushToServerV3_ApiKey()
        {
            var nugetexe = Util.GetNuGetExePath();
            var packagesDirectory = TestFilesystemUtility.CreateRandomTestFolder();

            try
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
                        var path = r.Url.AbsolutePath;

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
                            var h = r.Headers["X-NuGet-ApiKey"];
                            if (!h.Equals("blah-blah", StringComparison.OrdinalIgnoreCase))
                            {
                                return HttpStatusCode.Unauthorized;
                            }

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
                            serverV3.Uri + "index.json",
                            "-ApiKey",
                            "blah-blah"
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
            finally
            {
                // Cleanup
                TestFilesystemUtility.DeleteRandomTestFolders(packagesDirectory);
            }
        }

        [Fact]
        public void PushCommand_PushToServerV3_ApiKeyFromConfig()
        {
            var nugetexe = Util.GetNuGetExePath();
            var randomTestFolder = TestFilesystemUtility.CreateRandomTestFolder();

            try
            {
                // Arrange
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", randomTestFolder);
                string outputFileName = Path.Combine(randomTestFolder, "t1.nupkg");

                // Server setup
                var indexJson = Util.CreateIndexJson();

                using (var serverV3 = new MockServer())
                {
                    serverV3.Get.Add("/", r =>
                    {
                        var path = r.Url.AbsolutePath;

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
                            var h = r.Headers["X-NuGet-ApiKey"];
                            if (string.IsNullOrEmpty(h)
                            || !h.Equals("blah-blah", StringComparison.OrdinalIgnoreCase))
                            {
                                return HttpStatusCode.Unauthorized;
                            }

                            byte[] buffer = MockServer.GetPushedPackage(r);
                            using (var of = new FileStream(outputFileName, FileMode.Create))
                            {
                                of.Write(buffer, 0, buffer.Length);
                            }

                            return HttpStatusCode.Created;
                        });

                        serverV3.Start();
                        serverV2.Start();

                        var config = string.Format(@"<?xml version='1.0' encoding='utf-8'?>
                                                    <configuration>
                                                      <packageSources>
                                                        <add key='nuget.org' value='{0}' protocolVersion='3' />
                                                        <add key='nuget.org' value='{1}' />
                                                      </packageSources>
                                                      <packageRestore>
                                                        <add key='enabled' value='True' />
                                                        <add key='automatic' value='True' />
                                                      </packageRestore>
                                                      <disabledPackageSources />
                                                      <apikeys>
                                                        <add key='{2}' value='{3}' />
                                                      </apikeys>
                                                    </configuration>",
                                                    serverV3.Uri + "index.json",
                                                    serverV2.Uri + "push",
                                                    serverV2.Uri + "push",
                                                    Configuration.EncryptionUtility.EncryptString("blah-blah"));

                        var configFileName = Path.Combine(randomTestFolder, "nuget.config");
                        File.WriteAllText(configFileName, config);

                        // Act
                        string[] args = new string[]
                        {
                            "push",
                            packageFileName,
                            "-Source",
                            serverV3.Uri + "index.json",
                            "-ConfigFile",
                            configFileName
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
            finally
            {
                // Cleanup
                TestFilesystemUtility.DeleteRandomTestFolders(randomTestFolder);
            }
        }

        [Fact]
        public void PushCommand_DefaultPushSource()
        {
            var nugetexe = Util.GetNuGetExePath();
            var randomDirectory = TestFilesystemUtility.CreateRandomTestFolder();

            try
            {
                // Arrange
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", randomDirectory);
                string outputFileName = Path.Combine(randomDirectory, "t1.nupkg");

                // Server setup
                using (var serverV2 = new MockServer())
                {
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

                    serverV2.Start();

                    var config = string.Format(@"<?xml version='1.0' encoding='utf-8'?>
                                                    <configuration>
                                                      <packageSources>
                                                        <add key='nuget.org' value='{0}' />
                                                      </packageSources>
                                                      <packageRestore>
                                                        <add key='enabled' value='True' />
                                                        <add key='automatic' value='True' />
                                                      </packageRestore>
                                                      <config>
                                                        <add key='DefaultPushSource' value='{1}' />
                                                      </config>
                                                    </configuration>",
                                                serverV2.Uri + "push",
                                                serverV2.Uri + "push");

                    string configFileName = Path.Combine(randomDirectory, "nuget.config");
                    File.WriteAllText(configFileName, config);

                    // Act
                    string[] args = new string[]
                    {
                            "push",
                            packageFileName,
                            "-ConfigFile",
                            configFileName,
                            "-ApiKey",
                            "blah-blah"
                    };

                    var result = CommandRunner.Run(
                                    nugetexe,
                                    Directory.GetCurrentDirectory(),
                                    string.Join(" ", args),
                                    true);
                    serverV2.Stop();

                    // Assert
                    Assert.True(0 == result.Item1, result.Item2 + " " + result.Item3);
                    var output = result.Item2;
                    Assert.Contains("Your package was pushed.", output);
                    AssertFileEqual(packageFileName, outputFileName);
                }
            }
            finally
            {
                // Cleanup
                TestFilesystemUtility.DeleteRandomTestFolders(randomDirectory);
            }
        }

        [Fact]
        public void PushCommand_APIV2Package_Endpoint()
        {
            var nugetexe = Util.GetNuGetExePath();
            var tempPath = Path.GetTempPath();
            var packageDirectory = Path.Combine(tempPath, Guid.NewGuid().ToString());

            try
            {
                // Arrange
                Util.CreateDirectory(packageDirectory);
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                string outputFileName = Path.Combine(packageDirectory, "t1.nupkg");

                using (var server = new MockServer())
                {
                    server.Get.Add("/", r =>
                    {
                        var path = r.Url.AbsolutePath;

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
                        var path = r.Url.AbsolutePath;

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
                        server.Uri + "/api/v2/Package"
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
            finally
            {
                // Cleanup
                Util.DeleteDirectory(packageDirectory);
            }
        }

        [Theory]
        [InlineData("invalid")]
        public void PushCommand_InvalidInput_NonSource(string invalidInput)
        {
            var nugetexe = Util.GetNuGetExePath();
            var packagesDirectory = TestFilesystemUtility.CreateRandomTestFolder();

            try
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
            finally
            {
                TestFilesystemUtility.DeleteRandomTestFolders(packagesDirectory);
            }
        }

        [Theory]
        [InlineData("https://invalid-2a0358f1-88f2-48c0-b68a-bb150cac00bd.org")]
        [InlineData("https://invalid-2a0358f1-88f2-48c0-b68a-bb150cac00bd.org/api/v2")]
        [InlineData("https://invalid-2a0358f1-88f2-48c0-b68a-bb150cac00bd.org/api/v2/Package")]
        public void PushCommand_InvalidInput_V2HttpSource(string invalidInput)
        {
            var nugetexe = Util.GetNuGetExePath();
            var packagesDirectory = TestFilesystemUtility.CreateRandomTestFolder();

            try
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
                        "The remote name could not be resolved: 'invalid-2a0358f1-88f2-48c0-b68a-bb150cac00bd.org'"),
                    "Expected error message not found in " + result.Item3
                    );
            }
            finally
            {
                TestFilesystemUtility.DeleteRandomTestFolders(packagesDirectory);
            }
        }

        [Theory]
        [InlineData("https://nuget.org/api/blah")]
        public void PushCommand_InvalidInput_V2_NonExistent(string invalidInput)
        {
            var nugetexe = Util.GetNuGetExePath();
            var packagesDirectory = TestFilesystemUtility.CreateRandomTestFolder();

            try
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
                        "The remote server returned an error: (404) Not Found."),
                    "Expected error message not found in " + result.Item3
                    );
            }
            finally
            {
                TestFilesystemUtility.DeleteRandomTestFolders(packagesDirectory);
            }
        }

        [Theory]
        [InlineData("https://invalid-2a0358f1-88f2-48c0-b68a-bb150cac00bd.org/v3/index.json")]
        public void PushCommand_InvalidInput_V3_NonExistent(string invalidInput)
        {
            var nugetexe = Util.GetNuGetExePath();
            var packagesDirectory = TestFilesystemUtility.CreateRandomTestFolder();

            try
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
                    result.Item3.Contains("An error occurred while sending the request."),
                    "Expected error message not found in " + result.Item3
                    );
            }
            finally
            {
                TestFilesystemUtility.DeleteRandomTestFolders(packagesDirectory);
            }
        }

        [Theory]
        [InlineData("https://api.nuget.org/v4/index.json")]
        public void PushCommand_InvalidInput_V3_NotFound(string invalidInput)
        {
            var nugetexe = Util.GetNuGetExePath();
            var packagesDirectory = TestFilesystemUtility.CreateRandomTestFolder();

            try
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
                    result.Item3.Contains("Response status code does not indicate success: 400 (Bad Request)."),
                    "Expected error message not found in " + result.Item3
                    );
            }
            finally
            {
                TestFilesystemUtility.DeleteRandomTestFolders(packagesDirectory);
            }
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
    }
}
