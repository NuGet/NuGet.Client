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
            Id = $"{typeof (ConsoleCredentialProvider).Name}_{Guid.NewGuid()}";
        }

        /// <summary>
        /// Unique identifier of this credential provider
        /// </summary>
        public string Id { get; }

        private IConsole Console { get; set; }

        public Task<CredentialResponse> Get(
            Uri uri,
            IWebProxy proxy,
            bool isProxy,
            bool isRetry,
            bool nonInteractive,
            CancellationToken cancellationToken)
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

                var cred = new CredentialResponse(credentials, CredentialStatus.Success);

                var task = Task.FromResult(cred);

                return task;
            }
        }
    }
}