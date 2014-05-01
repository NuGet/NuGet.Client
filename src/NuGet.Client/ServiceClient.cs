using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using NuGet.Client.Models;

namespace NuGet.Client
{
    /// <summary>
    /// Represents a client for a single NuGet Service
    /// </summary>
    public class ServiceClient
    {
        /// <summary>
        /// Gets the <see cref="ServiceDescription"/> associated with the service.
        /// </summary>
        public ServiceDescription Service { get; private set; }

        /// <summary>
        /// Gets the <see cref="NuGetRepository"/> associated with the service
        /// </summary>
        public NuGetRepository Repository { get; private set; }

        /// <summary>
        /// Creates a new <see cref="ServiceClient"/> from the specified description and repository
        /// </summary>
        /// <param name="service">A <see cref="ServiceDescription"/> object that describes the target service</param>
        /// <param name="repository">The <see cref="NuGetRepository"/> object that contains the service</param>
        public ServiceClient(ServiceDescription service, NuGetRepository repository)
        {
            Guard.NotNull(service, "service");
            Guard.NotNull(repository, "repository");

            Service = service;
            Repository = repository;
        }

        /// <summary>
        /// Begins a <see cref="ServiceInvocationContext"/> to provide context for tracing a single client-initiated operation
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be signalled to cancel the request</param>
        /// <returns>A <see cref="ServiceInvocationContext"/> to provide context for tracing a single client-initiated operation</returns>
        public ServiceInvocationContext StartInvocation(CancellationToken cancellationToken)
        {
            return Repository.CreateContext(cancellationToken);
        }
    }
}
