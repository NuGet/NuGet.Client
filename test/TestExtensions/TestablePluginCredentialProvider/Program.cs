// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text;
using Newtonsoft.Json;

namespace NuGet.Test.TestExtensions.TestablePluginCredentialProvider
{
    class Program
    {
        static int Main()
        {
            string stdin = GetInput();
            TestCredentialRequest request = JsonConvert.DeserializeObject<TestCredentialRequest>(stdin);

            var responseDelaySeconds = Environment.GetEnvironmentVariable(TestCredentialResponse.ResponseDelaySeconds);
            var responseShouldThrow = Environment.GetEnvironmentVariable(TestCredentialResponse.ResponseShouldThrow);
            var responseShouldAbort = Environment.GetEnvironmentVariable(TestCredentialResponse.ResponseShouldAbort);
            var responseAbortMessage = Environment.GetEnvironmentVariable(TestCredentialResponse.ResponseAbortMessage);
            var responseExitCode = Environment.GetEnvironmentVariable(TestCredentialResponse.ResponseExitCode);
            var responseUsername = Environment.GetEnvironmentVariable(TestCredentialResponse.ResponseUserName);
            var responsePassword = Environment.GetEnvironmentVariable(TestCredentialResponse.ResponsePassword);

            System.Threading.Thread.Sleep(ToInt(responseDelaySeconds));

            if (ToBool(responseShouldThrow))
            {
                throw new ApplicationException("Throwing test exception");
            }

            dynamic response = new TestCredentialResponse
            {
                Abort = ToBool(responseShouldAbort),
                AbortMessage = responseAbortMessage,
                Password = responsePassword,
                Username = responseUsername
            };

            Console.WriteLine(JsonConvert.SerializeObject(response));

            return ToInt(responseExitCode);
        }

        private static string GetInput()
        {
            var buffer = new StringBuilder();
            while(true)
            {
                var line = Console.ReadLine();
                if(string.IsNullOrWhiteSpace(line))
                {
                    break;
                }

                buffer.AppendLine(line);
            }

            var result = buffer.ToString();
            return string.IsNullOrWhiteSpace(result) ? "{}" : result;
        }

        private static bool ToBool(string s)
        {
            bool b;
            bool.TryParse(s, out b);
            return b;
        }

        private static int ToInt(string s)
        {
            int i;
            int.TryParse(s, out i);
            return i;
        }
    }
}
