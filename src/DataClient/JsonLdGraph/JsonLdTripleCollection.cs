using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Data
{
    public class JsonLdTripleCollection : IEnumerable<JsonLdTriple>
    {
        private readonly IEnumerable<JsonLdTriple> _tripleCollection;

        public JsonLdTripleCollection(IEnumerable<JsonLdTriple> triples)
        {
            _tripleCollection = triples;
        }

        public virtual IEnumerable<JsonLdTriple> Triples
        {
            get
            {
                return _tripleCollection;
            }
        }

        public virtual int Count
        {
            get
            {
                return Triples.Count();
            }
        }

        public IEnumerator<JsonLdTriple> GetEnumerator()
        {
            return _tripleCollection.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return _tripleCollection.GetEnumerator();
        }
    }
}
