using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Common;

namespace NuGet.ProjectModel
{
    public class AssetsLogMessage : RestoreLogMessage
    {

        public AssetsLogMessage(LogLevel logLevel, NuGetLogCode errorCode,
            string errorString, string targetGraph)
            :base(logLevel, errorCode, errorString, targetGraph)
        {
        }

        public AssetsLogMessage(LogLevel logLevel, NuGetLogCode errorCode, string errorString)
            : this(logLevel, errorCode, errorString, string.Empty)
        {

        }

        public AssetsLogMessage(LogLevel logLevel, string errorString)
            : this(logLevel, LogLevel.Error == logLevel ? NuGetLogCode.NU1000 : NuGetLogCode.NU1500, errorString, string.Empty)
        {
        }

        /// <summary>
        /// Used to convert the Log Message object into a dictionary that can be written into the assets file.
        /// The Dictionary is converted into a JObject by the LockFileFormat.
        /// </summary>
        /// <returns>Dictionary of the properties representing the Log Message.</returns>

        public JObject ToJObject()
        {
            var logJObject = new JObject()
            {
                [LogMessageProperties.CODE] = Enum.GetName(typeof(NuGetLogCode), Code),
                [LogMessageProperties.LEVEL] = Enum.GetName(typeof(LogLevel), Level)
            };

            if (Level == LogLevel.Warning)
            {
                logJObject[LogMessageProperties.WARNING_LEVEL] = WarningLevel.ToString();
            }

            if (FilePath != null)
            {
                logJObject[LogMessageProperties.FILE_PATH] = FilePath;
            }

            if (LineNumber >= 0)
            {
                logJObject[LogMessageProperties.LINE_NUMBER] = LineNumber;
            }

            if (ColumnNumber >= 0)
            {
                logJObject[LogMessageProperties.COLUMN_NUMBER] = ColumnNumber;
            }

            if (Message != null)
            {
                logJObject[LogMessageProperties.MESSAGE] = Message;
            }

            if (TargetGraphs != null && TargetGraphs.Any() && TargetGraphs.All(l => !string.IsNullOrEmpty(l)))
            {
                logJObject[LogMessageProperties.TARGET_GRAPH] = new JArray(TargetGraphs);
            }

            return logJObject;
        }

        /// <summary>
        /// Used to convert the Log Message object into a dictionary that can be written into the assets file.
        /// The Dictionary is converted into a JObject by the LockFileFormat.
        /// </summary>
        /// <returns>Dictionary of the properties representing the Log Message.</returns>
        public Task<JObject> ToJObjectAsync()
        {
            return Task.FromResult(ToJObject());
        }

        public static AssetsLogMessage ParseJObject(JObject json)
        {
            AssetsLogMessage assetsLogMessage = null;

            var levelJson = json[LogMessageProperties.LEVEL];
            var codeJson = json[LogMessageProperties.CODE];
            var warningLevelJson = json[LogMessageProperties.WARNING_LEVEL];
            var filePathJson = json[LogMessageProperties.FILE_PATH];
            var lineNumberJson = json[LogMessageProperties.LINE_NUMBER];
            var columnNumberJson = json[LogMessageProperties.COLUMN_NUMBER];
            var messageJson = json[LogMessageProperties.MESSAGE];

            var isValid = true;

            isValid &= Enum.TryParse(levelJson.Value<string>(), out LogLevel level);
            isValid &= Enum.TryParse(codeJson.Value<string>(), out NuGetLogCode code);

            if (isValid)
            {
                assetsLogMessage = new AssetsLogMessage(level, code, messageJson.Value<string>())
                {
                    TargetGraphs = ParseJArray(json[LogMessageProperties.TARGET_GRAPH] as JArray)
                };

                if (level == LogLevel.Warning)
                {
                    assetsLogMessage.WarningLevel = (WarningLevel) Enum.Parse(typeof(WarningLevel), 
                        warningLevelJson.Value<string>());
                }

                if (filePathJson != null)
                {
                    assetsLogMessage.FilePath = filePathJson.Value<string>();
                }

                if (lineNumberJson != null)
                {
                    assetsLogMessage.LineNumber = lineNumberJson.Value<int>();
                }

                if (columnNumberJson != null)
                {
                    assetsLogMessage.ColumnNumber = columnNumberJson.Value<int>();
                }

                if (messageJson != null)
                {
                    assetsLogMessage.Message = messageJson.Value<string>();
                }
            }

            return assetsLogMessage;
        }

        private static IReadOnlyList<string> ParseJArray(JArray targetGraphsJson)
        {
            if (targetGraphsJson == null)
            {
                return new List<string>();
            }
            var items = new List<string>();
            foreach (var child in targetGraphsJson)
            {
                var item = child.Value<string>();
                if (item != null)
                {
                    items.Add(item);
                }
            }
            return items;
        }
    }
}
