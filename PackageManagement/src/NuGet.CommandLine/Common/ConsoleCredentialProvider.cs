using System;
using System.Net;
using System.Security;
using NuGet.Common;

namespace NuGet
{
    public class ConsoleCredentialProvider : ICredentialProvider
    {
        public ConsoleCredentialProvider(IConsole console)
        {
            Console = console;
        }

        private IConsole Console { get; set; }

        public ICredentials GetCredentials(Uri uri, IWebProxy proxy, CredentialType credentialType, bool retrying)
        {
            if (uri == null)
            {
                throw new ArgumentNullException("uri");
            }

            string message = credentialType == CredentialType.ProxyCredentials ?
                    LocalizedResourceManager.GetString("Credentials_ProxyCredentials") :
                    LocalizedResourceManager.GetString("Credentials_RequestCredentials");
            Console.WriteLine(message, uri.OriginalString);
            Console.Write(LocalizedResourceManager.GetString("Credentials_UserName"));
            string username = Console.ReadLine();
            Console.Write(LocalizedResourceManager.GetString("Credentials_Password"));

            using (SecureString password = new SecureString())
            {
                Console.ReadSecureString(password);
                var credentials = new NetworkCredential
                {
                    UserName = username,
                    SecurePassword = password
                };
                return credentials;
            }
        }
    }
}