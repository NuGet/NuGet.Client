// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NuGet.MicroBenchmarks
{
    public class CSVWriter : IDisposable
    {
        private List<(string eventName, long milliseconds)> _data;
        private string _name;
        private bool _disposedValue;

        public CSVWriter(string name)
        {
            _data = new List<(string entryname, long milliseconds)>();
            _name = $"{name}.csv";
        }

        public void Write(string eventName, long milliseconds)
        {
            _data.Add((eventName, milliseconds));
        }

        private void WriteToFile()
        {
            var time = System.DateTime.UtcNow;
            File.AppendAllLines(_name, _data.Select(r => $"{time},{r.eventName},{r.milliseconds}").ToList());
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    WriteToFile();
                    _data.Clear();
                }
                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
