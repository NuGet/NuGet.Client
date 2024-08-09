// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.ContentModel
{
    public class ContentItem
    {
        public string Path { get; set; }

        internal Dictionary<string, object> _properties;

        public Dictionary<string, object> Properties
        {
            get => _properties ?? CreateDictionary();
            internal set => _properties = value;
        }

        internal object _assembly;
        internal object _locale;
        internal object _related;
        internal object _msbuild;
        internal object _tfm;
        internal object _rid;
        internal object _any;
        internal object _satelliteAssembly;
        internal object _codeLanguage;
        internal object _tfmRaw;

        internal bool TryGetValue(string key, out object value)
        {
            if (_properties != null)
            {
                return _properties.TryGetValue(key, out value);
            }

            bool found = true;
            switch (key)
            {
                case "tfm":
                    value = _tfm;
                    break;
                case "rid":
                    value = _rid;
                    break;
                case "any":
                    value = _any;
                    break;
                case "assembly":
                    value = _msbuild;
                    break;
                case "locale":
                    value = _locale;
                    break;
                case "msbuild":
                    value = _msbuild;
                    break;
                case "related":
                    value = _related;
                    break;
                case "satelliteAssembly":
                    value = _satelliteAssembly;
                    break;
                case "codeLanguage":
                    value = _codeLanguage;
                    break;
                case "tfm_raw":
                    value = _tfmRaw;
                    break;
                default:
                    value = null;
                    found = false;
                    break;
            }

            if (found && value != null)
            {
                return true;
            }

            return false;
        }

        internal void Add(string key, object value)
        {
            if (_properties != null)
            {
                _properties.Add(key, value);
            }

            switch (key)
            {
                case "tfm":
                    _tfm = value;
                    break;
                case "rid":
                    _rid = value;
                    break;
                case "any":
                    _any = value;
                    break;
                case "assembly":
                    _msbuild = value;
                    break;
                case "locale":
                    _locale = value;
                    break;
                case "msbuild":
                    _msbuild = value;
                    break;
                case "related":
                    _related = value;
                    break;
                case "satelliteAssembly":
                    _satelliteAssembly = value;
                    break;
                case "codeLanguage":
                    _codeLanguage = value;
                    break;
                case "tfm_raw":
                    _tfmRaw = value;
                    break;
                default:
                    Properties.Add(key, value); // A property we can't pack means we should be using the dictionary instead.
                    break;
            }
        }

        public override string ToString()
        {
            return Path;
        }

        private Dictionary<string, object> CreateDictionary()
        {
            var properties = new Dictionary<string, object>();
            if (_assembly != null) // We always initialize the dictionary with the packed values. 
            {
                properties.Add("assembly", _assembly);
            }
            if (_locale != null)
            {
                properties.Add("locale", _locale);
            }
            if (_related != null)
            {
                properties.Add("related", _related);
            }
            if (_msbuild != null)
            {
                properties.Add("msbuild", _msbuild);
            }
            if (_tfm != null)
            {
                properties.Add("tfm", _tfm);
            }
            if (_rid != null)
            {
                properties.Add("rid", _rid);
            }
            if (_any != null)
            {
                properties.Add("any", _any);
            }
            if (_satelliteAssembly != null)
            {
                properties.Add("satelliteAssembly", _satelliteAssembly);
            }
            if (_codeLanguage != null)
            {
                properties.Add("codeLanguage", _codeLanguage);
            }
            if (_tfmRaw != null)
            {
                properties.Add("tfm_raw", _tfmRaw);
            }
            _properties = properties;
            return properties;
        }
    }
}
