using Microsoft.Data.Sqlite;
using System.Data;

namespace jira_fhir_mcp.Services;

/// <summary>
/// Service for managing database connections and operations in readonly mode
/// </summary>
public class DatabaseService : IDisposable
{
    private readonly CliConfig _config;
    private readonly string _connectionString;
    private readonly Lock _lock = new();
    private SqliteConnection? _db;
    public SqliteConnection Db => _db ?? throw new InvalidOperationException("DatabaseService has not been initialized. Call InitializeAsync() first.");
    private bool _disposed = false;
    
    private static DatabaseService _instance = null!;
    public static DatabaseService Instance => _instance ?? throw new InvalidOperationException("DatabaseService has not been initialized. Call Initialize() first.");
    
    /// <summary>
    /// Initialize database service with configuration
    /// </summary>
    /// <param name="config">CLI configuration containing database path</param>
    public DatabaseService(CliConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _connectionString = $"Data Source={_config.DbPath};Mode=ReadOnly;Cache=Shared";
    }

    /// <summary>
    /// Initialize the database connection - should be called once during service startup
    /// </summary>
    public void Initialize()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(DatabaseService));

        lock (_lock)
        {
            if (_db == null)
            {
                _db = new SqliteConnection(_connectionString);
                _db.Open();
            }
        }

        _instance = this;
    }

    /// <summary>
    /// Get database information
    /// </summary>
    public string DatabasePath => _config.DbPath;

    /// <summary>
    /// Dispose of database resources
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            lock (_lock)
            {
                _db?.Dispose();
                _db = null;
            }
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Finalizer
    /// </summary>
    ~DatabaseService()
    {
        Dispose();
    }
}