
# HL7 / FHIR Jira Utilities

This repo contains tools and utilities to ease Jira ticket processing for FHIR.

## Project Structure

This project uses a .NET 9 solution structure:

- `src/jira-fhir-cli/` - Command-line interface for database operations
- `src/jira-fhir-mcp/` - Model Context Protocol (MCP) server for AI integrations
- `src/JiraFhirUtils.Common/` - Shared database models and utilities
- `src/JiraFhirUtils.SQLiteGenerator/` - Source generator for SQLite table mappings
- `src/fmg-r6-review/` - FMG R6 review utilities

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- SQLite database with Jira issues (see Database Setup below)

## Building

```sh
dotnet build
```

## CLI Tool (`jira-fhir-cli`)

The CLI tool provides commands for loading, indexing, and searching Jira issues.

### Available Commands

| Command | Description |
|---------|-------------|
| `download` | Download JIRA XML files by week starting from current week working backwards |
| `load-xml` | Load JIRA issues from XML export files into the database |
| `build-fts` | Create full-text search (FTS5) index tables in the database |
| `extract-keywords` | Extract and index keywords from issues for BM25 search |
| `search-bm25` | Search issues using BM25 ranking algorithm |
| `summarize` | Generate AI summaries of issues using an LLM provider |
| `fix-scores` | Recalculate IDF and BM25 scores using existing frequency counts |

### Command Details

#### `download` - Download JIRA XML Files

Download issues directly from JIRA using the REST API. Requires authentication via cookie.

```sh
dotnet run --project src/jira-fhir-cli -- download --cookie "YOUR_JIRA_COOKIE" --jira-xml-dir ./downloads
```

Options:
- `--cookie`, `--jira-cookie` - JIRA authentication cookie (required)
- `--jira-xml-dir`, `--initial-dir` - Directory to save XML files (default: `bulk`)
- `--specification`, `--spec` - Optional JIRA specification filter
- `--download-limit` - Optional limit on number of days to download

#### `load-xml` - Load XML into Database

Load JIRA XML export files into a SQLite database.

```sh
dotnet run --project src/jira-fhir-cli -- load-xml --db-path ./jira_issues.sqlite --jira-xml-dir ./bulk
```

Options:
- `--db-path` - Path to SQLite database file (default: `jira_issues.sqlite`)
- `--jira-xml-dir`, `--initial-dir` - Directory containing XML files (default: `bulk`)
- `--drop-tables` - Drop existing tables before loading (default: false)
- `--keep-custom-field-source` - Keep source table for custom fields (default: false)

#### `build-fts` - Create Full-Text Search Tables

Create SQLite FTS5 full-text search tables for issues and comments.

```sh
dotnet run --project src/jira-fhir-cli -- build-fts --db-path ./jira_issues.sqlite
```

Options:
- `--db-path` - Path to SQLite database file (default: `jira_issues.sqlite`)

Example FTS queries after setup:
```sql
-- Search issues
SELECT * FROM issues_fts WHERE issues_fts MATCH 'search term';

-- Phrase search
SELECT * FROM issues_fts WHERE issues_fts MATCH '"exact phrase"';

-- Field-specific search
SELECT * FROM issues_fts WHERE title MATCH 'search term';
```

#### `extract-keywords` - Extract Keywords for BM25

Extract keywords from issues for BM25-based search. Includes FHIR-specific processing to preserve domain terminology.

```sh
dotnet run --project src/jira-fhir-cli -- extract-keywords --db-path ./jira_issues.sqlite
```

Options:
- `--db-path` - Path to SQLite database file (default: `jira_issues.sqlite`)
- `--fhir-spec-database` - Path to FHIR specification database for enhanced keyword extraction
- `--keyword-database` - Path to auxiliary database with stop words (default: `auxiliary.sqlite`)

#### `search-bm25` - BM25 Search

Search issues using the BM25 ranking algorithm.

```sh
dotnet run --project src/jira-fhir-cli -- search-bm25 --query "patient resource validation" --top-k 20
```

Options:
- `--query` - Search query (required)
- `--db-path` - Path to SQLite database file (default: `jira_issues.sqlite`)
- `--top-k`, `--limit` - Number of results to return (default: 20)
- `--keyword-type` - Filter by keyword type (Word, FhirElementPath, FhirOperationName)
- `--bm25-k1` - BM25 k1 parameter for term frequency saturation (default: 1.2)
- `--bm25-b` - BM25 b parameter for length normalization (default: 0.75)
- `--show-keywords` - Show top keywords by IDF score

#### `summarize` - Generate AI Summaries

Generate AI summaries of issues using various LLM providers.

```sh
dotnet run --project src/jira-fhir-cli -- summarize --db-path ./jira_issues.sqlite --llm-provider openai --llm-api-key "YOUR_API_KEY"
```

Options:
- `--db-path` - Path to SQLite database file (default: `jira_issues.sqlite`)
- `--llm-provider` - LLM provider: `openai`, `openrouter`, `lmstudio`, `azureopenai`, `ollama`
- `--llm-api-key` - API key (can also use environment variables or user secrets)
- `--llm-endpoint` - Custom API endpoint URL
- `--llm-model` - Model name to use
- `--llm-temperature` - Temperature setting (default: 0.3)
- `--llm-max-tokens` - Maximum response tokens (default: 1000)
- `--batch-size` - Issues per batch (default: 100)
- `--overwrite` - Overwrite existing summaries

For Azure OpenAI:
- `--llm-deployment-name` - Azure deployment name
- `--llm-resource-name` - Azure resource name
- `--llm-api-version` - Azure API version

#### `fix-scores` - Recalculate BM25 Scores

Recalculate IDF and BM25 scores using existing frequency counts.

```sh
dotnet run --project src/jira-fhir-cli -- fix-scores --db-path ./jira_issues.sqlite
```

Options:
- `--db-path` - Path to SQLite database file (default: `jira_issues.sqlite`)
- `--bm25-k1` - BM25 k1 parameter (default: 1.2)
- `--bm25-b` - BM25 b parameter (default: 0.75)
- `--show-progress` - Show progress during recalculation (default: true)

## MCP Server (`jira-fhir-mcp`)

The MCP server provides read-only access to the JIRA issues database for AI tools like Claude Code.

### Features

- Browse work queues with filtering by project, work group, resolution, status, and assignee
- Full-text search across issues, URLs, artifacts, pages, titles, and summaries
- Retrieve detailed issue information including custom fields and comments
- Find related issues using keyword similarity
- List all projects and work groups in the database

### Running the MCP Server

Start the HTTP MCP server:

```sh
dotnet run --project src/jira-fhir-mcp -- mcp-http --port 5000 --db-path ./jira_issues.sqlite
```

Options:
- `--port`, `-p` - HTTP server port (default: 5000)
- `--url` - Public URL for accessing the server
- `--db-path` - Path to SQLite database file (default: `jira_issues.sqlite`)
- `--fhir-spec-database` - Path to FHIR specification database

The MCP endpoint is available at `http://localhost:5000/mcp`.

### Claude Code Integration

To use the MCP server with Claude Code, add it using the `claude mcp add` command:

```sh
claude mcp add FhirJira -- dotnet run --project src/jira-fhir-mcp -- mcp-http --db-path /path/to/jira_issues.sqlite
```

Or if you've built the project:

```sh
claude mcp add FhirJira -- ./src/jira-fhir-mcp/bin/Debug/net9.0/jira-fhir-mcp mcp-http --db-path /path/to/jira_issues.sqlite
```

## Database Setup

### Using Pre-built Archives

The repository includes `bulk.tar.gz` containing FHIR Core tickets.

1. Extract the archive:
   ```sh
   tar -xzf bulk.tar.gz
   ```

2. Load the XML files into a database:
   ```sh
   dotnet run --project src/jira-fhir-cli -- load-xml --db-path ./jira_issues.sqlite --jira-xml-dir ./bulk
   ```

3. Create full-text search tables:
   ```sh
   dotnet run --project src/jira-fhir-cli -- build-fts --db-path ./jira_issues.sqlite
   ```

4. (Optional) Extract keywords for BM25 search:
   ```sh
   dotnet run --project src/jira-fhir-cli -- extract-keywords --db-path ./jira_issues.sqlite
   ```

### Downloading Fresh Data

To download the latest issues from JIRA:

```sh
dotnet run --project src/jira-fhir-cli -- download --cookie "YOUR_JIRA_COOKIE" --jira-xml-dir ./downloads
```

Then load and index as described above.

## Database Path Resolution

All tools support automatic database discovery in these locations (in order):
1. Explicit path via `--db-path` option
2. Current working directory: `./jira_issues.sqlite`
3. Parent directory: `../jira_issues.sqlite`
4. Relative to executable location

## Configuration

### User Secrets

For sensitive configuration like API keys, use .NET User Secrets:

```sh
cd src/jira-fhir-cli
dotnet user-secrets set "LLM:openai:ApiKey" "your-api-key"
dotnet user-secrets set "LLM:azureopenai:ApiKey" "your-azure-key"
```

### Environment Variables

API keys can also be set via environment variables:
- `OPENAI_API_KEY`
- `OPENROUTER_API_KEY`
- `AZURE_OPENAI_API_KEY`

### Configuration File

Create an `appsettings.json` in the project directory:

```json
{
  "LLM": {
    "openai": {
      "ApiKey": "your-api-key"
    }
  }
}
```

## Quick Start

1. Build the solution:
   ```sh
   dotnet build
   ```

2. Extract and load initial data:
   ```sh
   tar -xzf bulk.tar.gz
   dotnet run --project src/jira-fhir-cli -- load-xml
   dotnet run --project src/jira-fhir-cli -- build-fts
   ```

3. (Optional) Set up keyword search:
   ```sh
   dotnet run --project src/jira-fhir-cli -- extract-keywords
   ```

4. Start the MCP server:
   ```sh
   dotnet run --project src/jira-fhir-mcp -- mcp-http
   ```

## License

See [LICENSE](LICENSE) file.
