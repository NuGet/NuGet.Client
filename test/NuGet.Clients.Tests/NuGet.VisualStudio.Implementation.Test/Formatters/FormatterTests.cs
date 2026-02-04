// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;
using Nerdbank.Streams;

namespace NuGet.VisualStudio.Implementation.Test.Formatters
{
    public abstract class FormatterTests
    {
        private readonly MessagePackSerializerOptions _options;

        protected FormatterTests()
        {
            IMessagePackFormatter[] formatters = Contracts.NuGetContractsServiceMessagePackRpcDescriptor.CreateMessagePackFormatters();
            var resolvers = new IFormatterResolver[] { MessagePackSerializerOptions.Standard.Resolver };

            _options = MessagePackSerializerOptions.Standard
                .WithSecurity(MessagePackSecurity.UntrustedData)
                .WithResolver(CompositeResolver.Create(formatters, resolvers));
        }

        protected T SerializeThenDeserialize<T>(IMessagePackFormatter<T> formatter, T expectedResult)
        {
            return SerializeThenDeserialize(formatter, expectedResult, _options);
        }

        protected T SerializeThenDeserialize<T>(IMessagePackFormatter<T> formatter, T expectedResult, MessagePackSerializerOptions options)
        {
            var sequence = new Sequence<byte>();
            var writer = new MessagePackWriter(sequence);

            formatter.Serialize(ref writer, expectedResult, options);

            writer.Flush();

            var reader = new MessagePackReader(sequence.AsReadOnlySequence);

            return formatter.Deserialize(ref reader, options);
        }
    }
}
