﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Test.Apex;
using Microsoft.Test.Apex.Services;
using Microsoft.Test.Apex.VisualStudio;
using Microsoft.Test.Apex.VisualStudio.NuGet;
using Xunit;

namespace NuGet.Tests.Apex
{
    public abstract class ApexBaseTestClass : IClassFixture<ApexTestRequirementsFixture>, IDisposable
    {
        private readonly Lazy<IVerifier> _lazyVerifier;
        private readonly Lazy<NuGetApexTestService> _nuGetPackageManagerTestService;

        public ApexBaseTestClass()
        {
            _lazyVerifier = new Lazy<IVerifier>(() => GetApexService<ITestLoggerFactoryService>().GetOrCreate("Testcase"));
            _nuGetPackageManagerTestService = new Lazy<NuGetApexTestService>(() => VisualStudio.Get<NuGetApexTestService>());
        }

        public abstract VisualStudioHost VisualStudio { get; }

        public IVerifier Verify
        {
            get { return _lazyVerifier.Value; }
        }

        public abstract TService GetApexService<TService>() where TService : class;

        public abstract void EnsureVisualStudioHost();

        public abstract void CloseVisualStudioHost();

        public virtual NuGetApexTestService GetNuGetTestService()
        {
            EnsureVisualStudioHost();
            return _nuGetPackageManagerTestService.Value;
        }

        public virtual void Dispose()
        {
            CloseVisualStudioHost();
            //test cleanup
        }
    }
}
