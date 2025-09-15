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
    private readonly object _lock = new();
    private SqliteConnection? _connection;
    private bool _disposed = false;

    /// <summary>
    /// Initialize database service with configuration
    /// </summary>
    /// <param name="config">CLI configuration containing database path</param>
    public DatabaseService(CliConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _connectionString = $"Data Source={_config.DbPath};Mode=ReadOnly";
    }

    /// <summary>
    /// Get or create database connection
    /// </summary>
    private SqliteConnection GetConnection()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(DatabaseService));

        lock (_lock)
        {
            if (_connection == null)
            {
                _connection = new SqliteConnection(_connectionString);
                _connection.Open();
            }
            else if (_connection.State != ConnectionState.Open)
            {
                _connection.Dispose();
                _connection = new SqliteConnection(_connectionString);
                _connection.Open();
            }

            return _connection;
        }
    }

    /// <summary>
    /// Execute a query and return results as a list of dictionaries
    /// </summary>
    /// <param name="query">SQL query to execute</param>
    /// <param name="parameters">Parameters for the query</param>
    /// <returns>List of records as dictionaries</returns>
    public async Task<List<Dictionary<string, object?>>> ExecuteQueryAsync(string query, params SqliteParameter[] parameters)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(DatabaseService));

        var connection = GetConnection();

        using var command = new SqliteCommand(query, connection);

        if (parameters.Length > 0)
        {
            command.Parameters.AddRange(parameters);
        }

        var results = new List<Dictionary<string, object?>>();

        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var record = new Dictionary<string, object?>();

            for (int i = 0; i < reader.FieldCount; i++)
            {
                var fieldName = reader.GetName(i);
                var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                record[fieldName] = value;
            }

            results.Add(record);
        }

        return results;
    }

    /// <summary>
    /// Execute a scalar query and return the first column of the first row
    /// </summary>
    /// <param name="query">SQL query to execute</param>
    /// <param name="parameters">Parameters for the query</param>
    /// <returns>Scalar result</returns>
    public async Task<object?> ExecuteScalarAsync(string query, params SqliteParameter[] parameters)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(DatabaseService));

        var connection = GetConnection();

        using var command = new SqliteCommand(query, connection);

        if (parameters.Length > 0)
        {
            command.Parameters.AddRange(parameters);
        }

        return await command.ExecuteScalarAsync();
    }

    /// <summary>
    /// Create a SqliteParameter with the given name and value
    /// </summary>
    /// <param name="name">Parameter name (with @ prefix)</param>
    /// <param name="value">Parameter value</param>
    /// <returns>SqliteParameter</returns>
    public static SqliteParameter CreateParameter(string name, object? value)
    {
        return new SqliteParameter(name, value ?? DBNull.Value);
    }

    /// <summary>
    /// Create multiple SqliteParameters from a dictionary
    /// </summary>
    /// <param name="parameters">Dictionary of parameter names and values</param>
    /// <returns>Array of SqliteParameters</returns>
    public static SqliteParameter[] CreateParameters(Dictionary<string, object?> parameters)
    {
        return parameters.Select(p => new SqliteParameter(p.Key.StartsWith('@') ? p.Key : $"@{p.Key}", p.Value ?? DBNull.Value)).ToArray();
    }

    /// <summary>
    /// Get database information
    /// </summary>
    public string DatabasePath => _config.DbPath;

    /// <summary>
    /// Test database connectivity
    /// </summary>
    /// <returns>True if database is accessible</returns>
    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            var result = await ExecuteScalarAsync("SELECT 1");
            return result != null && result.Equals(1L);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Dispose of database resources
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            lock (_lock)
            {
                _connection?.Dispose();
                _connection = null;
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