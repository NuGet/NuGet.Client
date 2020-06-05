// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;
using NuGet.Configuration;

namespace NuGet.PackageManagement.VisualStudio
{
    internal class NuGetRpcFormatterResolver : IFormatterResolver
    {
        public static readonly NuGetRpcFormatterResolver Instance = new NuGetRpcFormatterResolver();

        private NuGetRpcFormatterResolver()
        {
        }

        public IMessagePackFormatter<T> GetFormatter<T>()
        {
            return FormatterCache<T>.Formatter;
        }

        private static class FormatterCache<T>
        {
            public static readonly IMessagePackFormatter<T> Formatter;

            static FormatterCache()
            {
                if (typeof(PackageSource).IsAssignableFrom(typeof(T)))
                {
                    Formatter = (IMessagePackFormatter<T>)new PackageSourceFormatter();
                }
                else
                {
                    Formatter = StandardResolver.Instance.GetFormatter<T>();
                }
            }
        }
    }
}
