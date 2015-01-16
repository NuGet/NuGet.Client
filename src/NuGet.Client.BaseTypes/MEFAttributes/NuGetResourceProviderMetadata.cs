using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace NuGet.Client
{
    [MetadataAttribute]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class NuGetResourceProviderMetadata : ExportAttribute, INuGetResourceProviderMetadata
    {
        private readonly Type _resourceType;
        private readonly string _name;
        private readonly string[] _before;
        private readonly string[] _after;

        /// <summary>
        /// INuGetResourceProvider MEF attribute
        /// </summary>
        /// <param name="resourceType">Base type of resource provided</param>
        public NuGetResourceProviderMetadata(Type resourceType)
            : this(resourceType, string.Empty)
        {

        }

        /// <summary>
        /// INuGetResourceProvider MEF attribute
        /// </summary>
        /// <param name="resourceType">Base type of resource provided</param>
        /// <param name="name">Provider name for ordering purposes</param>
        public NuGetResourceProviderMetadata(Type resourceType, string name)
            : this(resourceType, name, new string[0])
        {

        }

        /// <summary>
        /// INuGetResourceProvider MEF attribute
        /// </summary>
        /// <param name="resourceType">Base type of resource provided</param>
        /// <param name="name">Provider name for ordering purposes</param>
        /// <param name="before">Provider this has priority over</param>
        public NuGetResourceProviderMetadata(Type resourceType, string name, string before)
            : this(resourceType, name, new string[] { before })
        {

        }

        /// <summary>
        /// INuGetResourceProvider MEF attribute
        /// </summary>
        /// <param name="resourceType">Base type of resource provided</param>
        /// <param name="name">Provider name for ordering purposes</param>
        /// <param name="before">Providers this has priority over</param>
        public NuGetResourceProviderMetadata(Type resourceType, string name, IEnumerable<string> before)
            : this(resourceType, name, before, new string[0])
        {

        }

        /// <summary>
        /// INuGetResourceProvider MEF attribute
        /// </summary>
        /// <param name="resourceType">Base type of resource provided</param>
        /// <param name="name">Provider name for ordering purposes</param>
        /// <param name="before">Providers this has priority over</param>
        /// <param name="after">Providers that get called before this one</param>
        public NuGetResourceProviderMetadata(Type resourceType, string name, IEnumerable<string> before, IEnumerable<string> after)
            : base(typeof(INuGetResourceProvider))
        {
            _name = name == null ? string.Empty : name;
            _resourceType = resourceType;
            _before = before == null ? new string[0] : before.ToArray();
            _after = after == null ? new string[0] : after.ToArray();
        }

        public Type ResourceType
        {
            get
            {
                return _resourceType;
            }
        }

        public string Name
        {
            get { return _name; }
        }

        public IEnumerable<string> Before
        {
            get { return _before; }
        }

        public IEnumerable<string> After
        {
            get { return _after; }
        }
    }
}
