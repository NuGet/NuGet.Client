using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Net;
using System.Security.Principal;
using System.Text;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class NuGetPushCommandTest
    {
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
                        " -Source " + server.Uri + "push -NonInteractive";
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

                    Assert.Equal(3, credentialForPutRequest.Count);
                    Assert.Equal("a:b", credentialForPutRequest[0]);
                    Assert.Equal("c:d", credentialForPutRequest[1]);
                    Assert.Equal("testuser:testpassword", credentialForPutRequest[2]);
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

                    Assert.Equal(3, credentialForPutRequest.Count);
                    Assert.Equal("a:b", credentialForPutRequest[0]);
                    Assert.Equal("c:d", credentialForPutRequest[1]);
                    Assert.Equal("testuser:testpassword", credentialForPutRequest[2]);
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

        // Asserts that the contents of two files are equal.
        void AssertFileEqual(string fileName1, string fileName2)
        {
            Assert.Equal(File.ReadAllBytes(fileName1), File.ReadAllBytes(fileName2));
        }
    }
}
