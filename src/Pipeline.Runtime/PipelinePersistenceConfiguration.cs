namespace Pipeline.Runtime;

/// <summary>
/// Identifies the storage backend shared by pipeline persistence and compatible processors.
/// </summary>
public enum PipelinePersistenceKind
{
    /// <summary>
    /// Process-local storage whose contents are lost when the process exits.
    /// </summary>
    InMemory,
    /// <summary>
    /// Durable SQL Server storage.
    /// </summary>
    SqlServer
}

/// <summary>
/// Describes the selected persistence backend and its optional SQL Server connection string.
/// </summary>
public sealed class PipelinePersistenceConfiguration
{
    /// <summary>
    /// Gets the selected storage backend.
    /// </summary>
    public PipelinePersistenceKind Kind { get; }

    /// <summary>
    /// Gets the SQL Server connection string, or null for in-memory persistence.
    /// </summary>
    public string? ConnectionString { get; }

    private PipelinePersistenceConfiguration(
        PipelinePersistenceKind kind,
        string? connectionString)
    {
        Kind = kind;
        ConnectionString = connectionString;
    }

    /// <summary>
    /// Gets the shared configuration for process-local, non-durable storage.
    /// </summary>
    public static PipelinePersistenceConfiguration InMemory { get; } =
        new(PipelinePersistenceKind.InMemory, null);

    /// <summary>
    /// Creates a SQL Server persistence configuration with a nonblank connection string.
    /// </summary>
    /// <param name="connectionString">The SQL Server connection string, or null for in-memory persistence.</param>
    /// <returns>The immutable persistence configuration.</returns>
    public static PipelinePersistenceConfiguration SqlServer(
        string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        return new PipelinePersistenceConfiguration(
            PipelinePersistenceKind.SqlServer,
            connectionString);
    }
}