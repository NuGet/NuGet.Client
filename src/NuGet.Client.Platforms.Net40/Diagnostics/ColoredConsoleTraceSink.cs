using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Client.Diagnostics
{
    public class ColoredConsoleTraceSink : ITraceSink
    {
        private const int PadLength = 5; // This is the max length of all the "prefixes" below.

        public void Enter(string invocationId, string methodName, string filePath, int line)
        {
            WritePrefix("enter", ConsoleColor.Gray);
            Console.WriteLine(String.Format(CultureInfo.CurrentCulture, Strings.ColoredConsoleTraceSink_Enter, methodName, filePath, line));
        }

        public void SendRequest(string invocationId, HttpRequestMessage request, string methodName, string filePath, int line)
        {
            WritePrefix("http", ConsoleColor.Magenta);
            Console.WriteLine(String.Format(CultureInfo.CurrentCulture, Strings.ColoredConsoleTraceTraceSink_SendRequest, methodName, filePath, line, request.Method.ToString().ToUpperInvariant(), request.RequestUri));
        }

        public void ReceiveResponse(string invocationId, HttpResponseMessage response, string methodName, string filePath, int line)
        {
            WritePrefix("http", ConsoleColor.Magenta);
            Console.WriteLine(String.Format(CultureInfo.CurrentCulture, Strings.ColoredConsoleTraceTraceSink_ReceiveResponse, methodName, filePath, line, (int)response.StatusCode, response.RequestMessage.RequestUri));
        }

        public void Error(string invocationId, Exception exception, string methodName, string filePath, int line)
        {
            WritePrefix("error", ConsoleColor.Red);
            Console.WriteLine(String.Format(CultureInfo.CurrentCulture, Strings.ColoredConsoleTraceTraceSink_Error, methodName, filePath, line, exception.ToString()));
        }

        public void Exit(string invocationId, string methodName, string filePath, int line)
        {
            WritePrefix("exit", ConsoleColor.Gray);
            Console.WriteLine(String.Format(CultureInfo.CurrentCulture, Strings.ColoredConsoleTraceSink_Exit, methodName, filePath, line));
        }

        public void JsonParseWarning(string invocationId, Newtonsoft.Json.Linq.JToken token, string warning, string methodName, string filePath, int line)
        {
            WritePrefix("warn", ConsoleColor.Yellow);
            Console.WriteLine(String.Format(CultureInfo.CurrentCulture, Strings.ColoredConsoleTraceTraceSink_JsonParseWarning, methodName, filePath, line, SystemTraceSink.GetFileInfo(token), warning));
        }

        private void WritePrefix(string prefix, ConsoleColor consoleColor)
        {
            var oldColor = Console.ForegroundColor;
            Console.ForegroundColor = consoleColor;
            Console.Write(prefix.PadRight(PadLength));
            Console.ForegroundColor = oldColor;
            Console.Write(": ");
        }
    }
}
