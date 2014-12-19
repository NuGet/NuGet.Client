using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Client
{
    /// <summary>
    /// Represents a resource provided by a server endpoint (V2 or V3).
    /// *TODOs: Add a trace ?
    /// </summary>
    public abstract class Resource
    {
        protected string _host;             
        public string Host
        {
            get
            {
                return _host;
            }           
        }            
    }
}
