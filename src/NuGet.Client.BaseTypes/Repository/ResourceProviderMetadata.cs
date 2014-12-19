using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Client
{
    [MetadataAttribute] 
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)] 
    public class ResourceProviderMetadata : ExportAttribute,IResourceProviderMetadata
    {
        public ResourceProviderMetadata(string resourceName,Type resourceType):base(typeof(ResourceProvider))
        {
            ProviderName = resourceName;
            ResourceType = resourceType;
        }

        public string ProviderName
        {
            get;
            private set;
        }
        public Type ResourceType
        {
            get;
            private set;
        }
    }
}
