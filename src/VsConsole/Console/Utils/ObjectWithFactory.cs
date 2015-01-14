namespace NuGetConsole
{
    /// <summary>
    /// An object produced by a factory.
    /// </summary>
    /// <typeparam name="T">The factory type.</typeparam>
    class ObjectWithFactory<T>
    {
        public T Factory { get; private set; }

        public ObjectWithFactory(T factory)
        {
            UtilityMethods.ThrowIfArgumentNull(factory);
            this.Factory = factory;
        }
    }
}
