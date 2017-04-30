using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Common
{
    public class RestoreLogMessage : IAssetsLogMessage
    {
        public LogLevel Level { get; set; }
        public NuGetLogCode Code { get; set; }
        public string Message { get; set; }
        public IReadOnlyList<string> TargetGraphs { get; set; }
        public DateTimeOffset Time { get; set; }
        public string ProjectPath { get; set; }

        /// <summary>
        /// Project or Package Id
        /// </summary>
        public string Id { get; set; }

        public RestoreLogMessage(LogLevel logLevel, NuGetLogCode errorCode, 
            string errorString, string targetGraph)
        {
            Level = logLevel;
            Code = errorCode;
            Message = errorString;
            Time = DateTimeOffset.Now;

            if (!string.IsNullOrEmpty(targetGraph))
            {
                TargetGraphs = new List<string>
                {
                    targetGraph
                };
            }
        }

        public RestoreLogMessage(LogLevel logLevel, NuGetLogCode errorCode, string errorString)
            : this (logLevel, errorCode, errorString, string.Empty)
        {

        }

        public RestoreLogMessage(LogLevel logLevel, string errorString)
            : this(logLevel, NuGetLogCode.NU1000, errorString, string.Empty)
        {

        }

        public IDictionary<string, object> ToDictionary()
        {
            var errorDictionary = new Dictionary<string, object>
            {
                [LogMessageProperties.CODE] = Enum.GetName(typeof(NuGetLogCode), Code),
                [LogMessageProperties.LEVEL] = Enum.GetName(typeof(LogLevel), Level)
            };

            if(Message != null)
            {
                errorDictionary[LogMessageProperties.MESSAGE] = Message;
            }

            if(TargetGraphs != null && TargetGraphs.Any() && TargetGraphs.All(l => !string.IsNullOrEmpty(l)))
            {
                errorDictionary[LogMessageProperties.TARGET_GRAPH] = TargetGraphs;
            }

            return errorDictionary;
        }


        public Task<IDictionary<string, object>> ToDictionaryAsync()
        {
            return Task.FromResult(ToDictionary());
        }

        public string FormatMessage()
        {
            var errorString = new StringBuilder();

            errorString.Append($"{Enum.GetName(typeof(NuGetLogCode), Code)}: {Message}");

            return errorString.ToString();
        }

        public Task<string> FormatMessageAsync()
        {
            return Task.FromResult(FormatMessage());
        }

        /// <summary>
        /// Create a log message for a target graph library.
        /// </summary>
        public static RestoreLogMessage CreateWarning(
            NuGetLogCode code,
            string id,
            string message,
            params string[] targetGraphs)
        {
            return new RestoreLogMessage(LogLevel.Warning, message)
            {
                Code = code,
                Id = id,
                TargetGraphs = targetGraphs.ToList()
            };
        }
    }
}
