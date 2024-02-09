namespace GitVersion.Configuration
{
    public interface IIgnoreConfiguration
    {
        DateTimeOffset? Before { get; }

        IReadOnlyCollection<string> Shas { get; }

        IPathFilterConfiguration PathFilters { get; }

        public bool IsEmpty => Before == null && Shas.Count == 0;
    }
}
