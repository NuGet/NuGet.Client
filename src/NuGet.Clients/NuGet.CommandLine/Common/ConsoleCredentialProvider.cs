using System;
using System.Net;
using System.Security;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Credentials;
using System.Threading;

namespace NuGet
{
    public class ConsoleCredentialProvider : Credentials.ICredentialProvider
    {
        public ConsoleCredentialProvider(IConsole console)
        {
            Console = console;
        }

        private IConsole Console { get; set; }

        public Task<ICredentials> Get(Uri uri, IWebProxy proxy, bool isProxy, bool isRetry,
            bool nonInteractive, CancellationToken cancellationToken)
        {
            if (uri == null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            string message = isProxy ?
                    LocalizedResourceManager.GetString("Credentials_ProxyCredentials") :
                    LocalizedResourceManager.GetString("Credentials_RequestCredentials");
            Console.WriteLine(message, uri.OriginalString);
            Console.Write(LocalizedResourceManager.GetString("Credentials_UserName"));
            cancellationToken.ThrowIfCancellationRequested();
            string username = Console.ReadLine();
            Console.Write(LocalizedResourceManager.GetString("Credentials_Password"));

            using (SecureString password = new SecureString())
            {
                cancellationToken.ThrowIfCancellationRequested();
                Console.ReadSecureString(password);
                var credentials = new NetworkCredential
                {
                    UserName = username,
                    SecurePassword = password
                };

                var task = Task.FromResult((ICredentials)credentials);

                return task;
            }
        }
    }
}