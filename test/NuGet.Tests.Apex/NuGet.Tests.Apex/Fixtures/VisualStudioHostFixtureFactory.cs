// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Tests.Apex.Platform;
using NuGet.Tests.Foundation.TestAttributes.Context;

namespace NuGet.Tests.Apex
{
    public class VisualStudioHostFixtureFactory : IVisualStudioHostFixtureFactory, IDisposable
    {
        private VisualStudioHostFixture _visualStudioHostFxiture;

        private Dictionary<Product, VisualStudioHostFixture> fixtures;

        public VisualStudioHostFixtureFactory()
        {
            fixtures = new Dictionary<Product, VisualStudioHostFixture>();
        }

        public VisualStudioHostFixture GetVisualStudioHostFixture()
        {
            if (_visualStudioHostFxiture == null)
            {
                _visualStudioHostFxiture = new VisualStudioHostFixture();
            }

            return _visualStudioHostFxiture;
        }

        public VisualStudioHostFixture GetVisualStudioHostFixtureForContext(Context context)
        {
            if (fixtures == null)
            {
                throw new ObjectDisposedException("this");
            }

            VisualStudioHostFixture fixture;

            if (!fixtures.TryGetValue(context.Product, out fixture))
            {
                fixture = new VisualStudioHostFixture();
                fixture.VisualStudioHostConfiguration.TargetSku = context.TargetSkuConfiguration();
                fixtures.Add(context.Product, fixture);
            }

            return fixture;
        }

        public virtual void Dispose()
        {
            if (_visualStudioHostFxiture != null)
            {
                _visualStudioHostFxiture.Dispose();
            }

            if (fixtures != null)
            {
                foreach (VisualStudioHostFixture fixture in fixtures.Values)
                {
                    IDisposable disposable = fixture as IDisposable;
                    if (disposable != null)
                    {
                        disposable.Dispose();
                    }
                }

                fixtures.Clear();
                fixtures = null;
            }
        }
    }
}
