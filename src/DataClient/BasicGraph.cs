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
    public class BasicGraph : BaseGraph
    {
        private readonly HashSet<Triple> _triples;

        public BasicGraph()
            : this(new HashSet<Triple>())
        {

        }

        public BasicGraph(IEnumerable<Triple> triples)
        {
            _triples = new HashSet<Triple>(triples);
        }

        public override HashSet<Triple> Triples
        {
            get
            {
                return _triples;
            }
        }

        
    }

    public class Triple : IEquatable<Triple>
    {
        private readonly RDFDataset.Node _subNode;
        private readonly RDFDataset.Node _predNode;
        private readonly RDFDataset.Node _objNode;

        public Triple(RDFDataset.Node subNode, RDFDataset.Node predNode, RDFDataset.Node objNode)
        {
            _subNode = subNode;
            _predNode = predNode;
            _objNode = objNode;
        }

        public RDFDataset.Node Subject
        {
            get
            {
                return _subNode;
            }
        }

        public RDFDataset.Node Predicate
        {
            get
            {
                return _predNode;
            }
        }

        public RDFDataset.Node Object
        {
            get
            {
                return _objNode;
            }
        }

        public bool Equals(Triple other)
        {
            return Subject.Equals(other.Subject) && Predicate.Equals(other.Predicate) && Object.Equals(other.Object);
        }
    }
}
