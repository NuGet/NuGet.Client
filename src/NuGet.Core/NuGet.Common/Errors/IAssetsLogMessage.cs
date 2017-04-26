using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Common
{
    public interface IAssetsLogMessage : ILogMessage
    {
        /// <summary>
        /// Used to convert the Log Message object into a dictionary that can be written into the assets file.
        /// The Dictionary is converted into a JObject by the LockFileFormat.
        /// </summary>
        /// <returns>Dictionary of the properties representing the Log Message.</returns>
        IDictionary<string, object> ToDictionary();

        /// <summary>
        /// Used to convert the Log Message object into a dictionary that can be written into the assets file.
        /// The Dictionary is converted into a JObject by the LockFileFormat.
        /// </summary>
        /// <returns>Dictionary of the properties representing the Log Message.</returns>
        Task<IDictionary<string, object>> ToDictionaryAsync();
    }
}
