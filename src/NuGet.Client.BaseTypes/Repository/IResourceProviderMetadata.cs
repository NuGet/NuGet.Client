using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Client
{
    public interface IResourceProviderMetadata
    {
         string ProviderName { get;} 
         Type ResourceType { get;} 
    }     
}
