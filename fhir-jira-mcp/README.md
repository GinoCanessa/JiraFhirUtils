# FHIR JIRA MCP Server

A Model Context Protocol (MCP) server for read-only access to the JIRA issues SQLite database. This server provides tools to browse work queues and search for related tickets.

The server supports both stdio (default) and HTTP transport modes, allowing flexible integration with MCP clients.

## Features

- **Browse Work Queue**: Filter issues by project key, work group, resolution, status, and/or assignee
- **Search Related Tickets**: Full-text search across issues matching related URLs, artifacts, pages, titles, and summaries
- **Get Issue Details**: Retrieve detailed information about specific issues including custom fields
- **Get Issue Comments**: Retrieve all comments for a specific issue
- **List Project Keys**: Get all unique project keys in the database
- **List Work Groups**: Get all unique work groups in the database

## Installation

1. Navigate to the `fhir-jira-mcp` directory:
   ```bash
   cd fhir-jira-mcp
   ```

2. Install dependencies:
   ```bash
   npm install
   ```

## Usage

### Running the Server

#### Stdio Mode (Default)
Start the MCP server in stdio mode for use with Claude Desktop or other stdio-based MCP clients:
```bash
npm start
```

#### HTTP Mode
Start the MCP server with an HTTP endpoint:
```bash
# Run on port 3000 (using npm script)
npm run start:http

# Or specify a custom port
node index.js --port 8080
# or
node index.js -p 8080
```

The server will start both stdio and HTTP transports when a port is specified. The HTTP endpoint will be available at `http://localhost:<port>/mcp`.

### Configuring with Claude Desktop

Add the following to your Claude Desktop configuration file (`claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "fhir-jira": {
      "command": "node",
      "args": ["/path/to/fhir-jira-mcp/index.js"],
      "env": {}
    }
  }
}
```

## HTTP API

When running in HTTP mode, the server exposes the following endpoints:

- `POST /mcp` - Main MCP endpoint for all tool requests
- `GET /health` - Health check endpoint

### Authentication
The HTTP server operates in stateless mode with no session management. All requests are independent.

### CORS
The server is configured with permissive CORS settings to allow requests from any origin.

### Example HTTP Request

```bash
# List issues
curl -X POST http://localhost:3000/mcp \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "method": "tools/call",
    "params": {
      "name": "list_issues",
      "arguments": {
        "project_key": "FHIR",
        "limit": 10
      }
    },
    "id": 1
  }'

# Search issues
curl -X POST http://localhost:3000/mcp \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "method": "tools/call",
    "params": {
      "name": "search_issues",
      "arguments": {
        "query": "patient resource",
        "limit": 5
      }
    },
    "id": 2
  }'
```

## Available MCP Tools

The FHIR JIRA MCP server provides the following tools for interacting with the JIRA issues database:

### 1. `list_issues`
**Description:** List issues filtered by project_key, work_group, resolution, status, and/or assignee

**Parameters:**
- `project_key` (optional): Filter by project key
- `work_group` (optional): Filter by work group
- `resolution` (optional): Filter by resolution
- `status` (optional): Filter by status
- `assignee` (optional): Filter by assignee
- `limit` (optional): Maximum number of results (default: 50)
- `offset` (optional): Offset for pagination (default: 0)

**Example:**
```json
{
  "tool": "list_issues",
  "arguments": {
    "project_key": "FHIR",
    "status": "In Progress",
    "limit": 20
  }
}
```

### 2. `search_issues`
**Description:** Search for tickets using SQLite FTS5 testing against issue fields

**Parameters:**
- `query` (required): Search query string
- `search_fields` (optional): Array of fields to search in. Options: `title`, `description`, `summary`, `resolution_description`. Defaults to all fields.
- `limit` (optional): Maximum number of results (default: 50)

**Example:**
```json
{
  "tool": "search_issues",
  "arguments": {
    "query": "patient resource",
    "search_fields": ["title", "summary"],
    "limit": 10
  }
}
```

### 3. `find_related_issues`
**Description:** Find issues related to a specific issue by key, using FTS5 for efficient searching

**Parameters:**
- `issue_key` (required): The issue key to find related issues for
- `keywords` (optional): Keywords to search for in related issues
- `limit` (optional): Maximum number of results (default: 50)

**Example:**
```json
{
  "tool": "find_related_issues",
  "arguments": {
    "issue_key": "FHIR-123",
    "keywords": "patient observation",
    "limit": 10
  }
}
```

### 4. `get_issue_details`
**Description:** Get detailed information about a specific issue by key

**Parameters:**
- `issue_key` (required): The issue key (e.g., "FHIR-123")

**Example:**
```json
{
  "tool": "get_issue_details",
  "arguments": {
    "issue_key": "FHIR-123"
  }
}
```

### 5. `get_issue_comments`
**Description:** Get comments for a specific issue

**Parameters:**
- `issue_key` (required): The issue key (e.g., "FHIR-123")

**Example:**
```json
{
  "tool": "get_issue_comments",
  "arguments": {
    "issue_key": "FHIR-123"
  }
}
```

### 6. `list_project_keys`
**Description:** List all unique project keys in the database

**Parameters:** None

**Example:**
```json
{
  "tool": "list_project_keys",
  "arguments": {}
}
```

### 7. `list_work_groups`
**Description:** List all unique work groups in the database

**Parameters:** None

**Example:**
```json
{
  "tool": "list_work_groups",
  "arguments": {}
}
```

## Database Schema

The server expects a SQLite database (`jira_issues.sqlite`) in the parent directory with the following main tables:

- `issues`: Main issue data including key, title, summary, status, resolution, assignee, etc.
- `comments`: Comments associated with issues
- `custom_fields`: Custom field values for issues (including work_group, related_url, etc.)
- `issues_fts`: Full-text search index for efficient searching

## Notes

- The server operates in read-only mode for database safety
- Full-text search uses SQLite's FTS5 extension for efficient querying
- All responses are returned as JSON-formatted text
- The database path is hardcoded to `../jira_issues.sqlite` relative to the server location
- Note that if you want to test the MCP Server, a tool such as [ModelContextProtocol/inspector](https://github.com/modelcontextprotocol/inspector) is useful:
```sh
npx @modelcontextprotocol/inspector
```

## Troubleshooting

If you encounter connection issues:
1. Ensure the `jira_issues.sqlite` file exists in the parent directory
2. Check that the database file has read permissions
3. Verify that all dependencies are installed correctly
4. Check the console output for error messages

## License

This MCP server is part of the JiraFhirUtils project.