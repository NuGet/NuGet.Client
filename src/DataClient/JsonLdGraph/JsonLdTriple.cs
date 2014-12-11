using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JsonLD.Core;
using Node = JsonLD.Core.RDFDataset.Node;
using System.Globalization;

namespace NuGet.Data
{
    /// <summary>
    /// A triple + page and JToken of the subject
    /// </summary>
    public class JsonLdTriple : Triple
    {
        private readonly JObject _jsonNode;
        private readonly JsonLdPage _jsonPage;

        public JsonLdTriple(JsonLdPage page, JObject jsonNode, Node subNode, Node predNode, Node objNode)
            : base(subNode, predNode, objNode)
        {
            _jsonNode = jsonNode;
            _jsonPage = page;
        }

        /// <summary>
        /// The original compacted token of the Subject node.
        /// </summary>
        public JObject JsonNode
        {
            get
            {
                return _jsonNode;
            }
        }

        /// <summary>
        /// True if this the entity came from a page with the same base url.
        /// </summary>
        /// <remarks>This would mean that everything is now known about the subject based on the NuGet graph rules.</remarks>
        public bool HasIdMatchingUrl
        {
            get
            {
                return Page.IsEntityFromPage(new Uri(Subject.GetValue()));
            }
        }

        /// <summary>
        /// The page this data originally came from.
        /// </summary>
        public JsonLdPage Page
        {
            get
            {
                return _jsonPage;
            }
        }

        /// <summary>
        /// True if the Page uri and JToken reference are also equal.
        /// </summary>
        public bool IsExactSame(JsonLdTriple other)
        {
            return JsonNode.Equals(other.JsonNode) && Page.Equals(other.Page) && base.Equals(other);
        }

        public override string ToString()
        {
            return String.Format(CultureInfo.InvariantCulture, "{0} {1} {2} {3} {4}", Subject.GetValue(), Predicate.GetValue(), Object.GetValue(), JsonNode == null ? "NoJson" : "Json", Page);
        }
    }
}
