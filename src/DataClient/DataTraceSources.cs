using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Data
{
    public static class DataTraceSources
    {
        public static readonly TraceSource DataClient = new TraceSource(typeof(DataClient).FullName);

        public static IEnumerable<TraceSource> GetAllSources()
        {
            return typeof(DataTraceSources).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => typeof(TraceSource).IsAssignableFrom(f.FieldType))
                .Select(f => (TraceSource)f.GetValue(null));
        }

        internal static void Verbose(string format, params string[] message)
        {
            DataClient.TraceEvent(TraceEventType.Verbose, 0, String.Format(CultureInfo.InvariantCulture, format, message));
        }

        internal static void Verbose(string message)
        {
            DataClient.TraceEvent(TraceEventType.Verbose, 0, message);
        }
    }
}
