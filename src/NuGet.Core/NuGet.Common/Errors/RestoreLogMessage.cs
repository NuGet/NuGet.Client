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
        public string ProjectPath { get; set; }
        public IReadOnlyList<string> TargetGraphs { get; set; }
        public DateTimeOffset Time { get; set; }

        public RestoreLogMessage(LogLevel logLevel, NuGetLogCode errorCode, 
            string errorString, string projectPath, string targetGraph)
        {
            Level = logLevel;
            Code = errorCode;
            Message = errorString;
            ProjectPath = projectPath;
            Time = DateTimeOffset.Now;

            if (!string.IsNullOrEmpty(targetGraph))
            {
                TargetGraphs = new List<string>
                {
                    targetGraph
                };
            }
        }

        public bool Equals(RestoreLogMessage other)
        {
            if(other == null)
            {
                return false;
            }
            else if(ReferenceEquals(this, other))
            {
                return true;
            }
            else if(Level == other.Level 
                && ProjectPath.Equals(other.ProjectPath) 
                && Level == other.Level
                && TargetGraphs.SequenceEqual(other.TargetGraphs))
            {
                return true;
            }
            else
            {
                return false;
            }
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

            errorString.Append($"{Enum.GetName(typeof(NuGetLogCode), Code)}:{Message}");

            return errorString.ToString();
        }

        public Task<string> FormatMessageAsync()
        {
            return Task.FromResult(FormatMessage());
        }
    }
}
