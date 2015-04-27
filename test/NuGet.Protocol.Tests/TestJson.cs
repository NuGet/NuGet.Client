using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataTest
{
    public static class TestJson
    {
        /// <summary>
        /// A basic graph
        /// </summary>
        public static JObject BasicGraph
        {
            get
            {
                return JObject.Parse(@"{
                  ""@context"": {
                    ""@vocab"": ""http://schema.org/test#"",
                    ""test"": ""http://schema.org/test#"",
                    ""items"": {
                      ""@id"": ""test#Child"",
                      ""@container"": ""@set""
                    },
                    ""hasProperty"": {
                      ""@container"": ""@set""
                    },
                    ""xsd"": ""http://www.w3.org/2001/XMLSchema#""
                  },
                  ""@id"": ""http://test/doc"",
                  ""@type"": ""Main"",
                  ""test:items"": [
                    {
                      ""@id"": ""http://test/doc#a"",
                      ""@type"": ""Child"",
                      ""test:info"": {
                        ""@id"": ""http://test/doc#c"",
                        ""@type"": ""PartialEntity"",
                        ""name"": ""grandChildC"",
                        ""partialEntityDescription"": {
                          ""@id"": ""http://test/doc#grandChildDescription"",
                          ""@type"": ""test:PartialEntityDescription"",
                          ""hasProperty"": [
                            ""http://schema.test#name"",
                            ""http://schema.test#title""
                          ]
                        }
                      },
                      ""test:name"": ""childA""
                    },
                    {
                      ""@id"": ""http://test/doc#b"",
                      ""@type"": ""Child"",
                      ""info"": {
                        ""@id"": ""http://test/doc#d"",
                        ""@type"": ""GrandChild""
                      },
                      ""name"": ""childB""
                    }
                  ],
                  ""name"": ""test""
                }");
            }
        }

        /// <summary>
        /// A basic graph
        /// </summary>
        public static JObject BasicGraph2
        {
            get
            {
                return JObject.Parse(@"{
                  ""@context"": {
                    ""@vocab"": ""http://schema.org/test#"",
                    ""test"": ""http://schema.org/test#"",
                    ""items"": {
                      ""@id"": ""test#Child"",
                      ""@container"": ""@set""
                    },
                    ""hasProperty"": {
                      ""@container"": ""@set""
                    },
                    ""xsd"": ""http://www.w3.org/2001/XMLSchema#""
                  },
                  ""@id"": ""http://test/doc2"",
                  ""@type"": ""Main"",
                  ""test:items"": [
                    {
                      ""@id"": ""http://test/doc2#a"",
                      ""@type"": ""Child"",
                      ""test:info"": {
                        ""@id"": ""http://test/doc2#c"",
                        ""@type"": ""PartialEntity"",
                        ""name"": ""grandChildC"",
                        ""partialEntityDescription"": {
                          ""@id"": ""http://test/doc2#grandChildDescription"",
                          ""@type"": ""test:PartialEntityDescription"",
                          ""hasProperty"": [
                            ""http://schema.test#name"",
                            ""http://schema.test#title""
                          ]
                        }
                      },
                      ""test:name"": ""childA""
                    },
                    {
                      ""@id"": ""http://test/doc2#b"",
                      ""@type"": ""Child"",
                      ""info"": {
                        ""@id"": ""http://test/doc2#d"",
                        ""@type"": ""GrandChild""
                      },
                      ""name"": ""childB""
                    }
                  ],
                  ""name"": ""test""
                }");
            }
        }


        /// <summary>
        /// BasicGraph only with only the ids
        /// </summary>
        public static JObject BasicGraphBare
        {
            get
            {
                return JObject.Parse(@"{
                  ""@context"": {
                    ""@vocab"": ""http://schema.org/test#"",
                    ""test"": ""http://schema.org/test#"",
                    ""items"": {
                      ""@id"": ""test#Child"",
                      ""@container"": ""@set""
                    },
                    ""hasProperty"": {
                      ""@container"": ""@set""
                    },
                    ""xsd"": ""http://www.w3.org/2001/XMLSchema#""
                  },
                  ""@id"": ""http://test/docBare"",
                  ""@type"": ""Main"",
                  ""test:items"": [
                    {
                      ""@id"": ""http://test/doc#a"",
                      ""@type"": ""Child""
                    },
                    {
                      ""@id"": ""http://test/doc#b"",
                      ""@type"": ""Child""
                    }
                  ],
                  ""name"": ""test bare""
                }");
            }
        }

        /// <summary>
        /// a graph with no @id at the root
        /// </summary>
        public static JObject BlankNode
        {
            get
            {
                return JObject.Parse(@"{
                  ""@context"": {
                    ""@vocab"": ""http://schema.org/test#"",
                    ""test"": ""http://schema.org/test#"",
                    ""items"": {
                      ""@id"": ""test#Child"",
                      ""@container"": ""@set""
                    },
                    ""hasProperty"": {
                      ""@container"": ""@set""
                    },
                    ""xsd"": ""http://www.w3.org/2001/XMLSchema#""
                  },
                  ""test:items"": [
                    {
                      ""@id"": ""http://test/doc#a"",
                      ""@type"": ""Child""
                    },
                    {
                      ""@id"": ""http://test/doc#b"",
                      ""@type"": ""Child""
                    }
                  ],
                  ""name"": ""test bare""
                }");
            }
        }

        /// <summary>
        /// normal json
        /// </summary>
        public static JObject NonRDF
        {
            get
            {
                return JObject.Parse(@"{
                  ""items"": [
                    {
                      ""a"": ""b""
                    },
                    {
                      ""c"": ""d""
                    }
                  ],
                  ""name"": ""test non rdf""
                }");
            }
        }
    }
}
