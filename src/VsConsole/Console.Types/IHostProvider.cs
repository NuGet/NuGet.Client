namespace NuGetConsole
{
    /// <summary>
    /// MEF interface for host provider. PowerConsole host providers must export this
    /// interface implementation and decorate it with a HostName attribute.
    /// </summary>
    public interface IHostProvider
    {
        /// <summary>
        /// Create a new host instance.
        /// </summary>
        /// <param name="async">Indicates whether to create asynchronous or synchronous host.</param>
        /// <returns>A new host instance.</returns>
        IHost CreateHost(bool @async);
    }
}
