# List Issues Tool

## Description
Lists JIRA issues with comprehensive filtering options including project, workgroup, status, type, priority, assignee, reporter, and more. Supports pagination and sorting.

## Tool Name
`list_issues`

## Arguments

### Basic Filters
- `project` (string): Filter by project key
- `workgroup` (string): Filter by work group
- `status` (string): Filter by status
- `resolution` (string): Filter by resolution
- `type` (string): Filter by issue type
- `priority` (string): Filter by priority
- `assignee` (string): Filter by assignee
- `reporter` (string): Filter by reporter
- `specification` (string): Filter by specification
- `vote` (string): Filter by vote status
- `grouping` (string): Filter by grouping

### Pagination
- `limit` (number): Maximum number of results (1-1000, default: 50)
- `offset` (number): Pagination offset (default: 0)

### Sorting
- `sort` (string): Sort field (id, key, created, updated, priority)
- `order` (string): Sort order (asc or desc, default: desc)

### Date Range Filters
All date parameters accept ISO 8601 format (e.g., '2023-12-01T10:30:00Z' or '2023-12-01')

- `created_after` (string): Filter by creation date (ISO 8601)
- `created_before` (string): Filter by creation date (ISO 8601)
- `updated_after` (string): Filter by update date (ISO 8601)
- `updated_before` (string): Filter by update date (ISO 8601)
- `resolved_after` (string): Filter by resolution date (ISO 8601)
- `resolved_before` (string): Filter by resolution date (ISO 8601)

## Response Format

```json
{
  "total": 150,
  "returned": 50,
  "offset": 0,
  "limit": 50,
  "hasMore": true,
  "issues": [
    {
      "Id": 12345,
      "Key": "FHIR-12345",
      "Title": "Issue Title",
      "IssueUrl": "https://jira.hl7.org/browse/FHIR-12345",
      "ProjectId": 10000,
      "ProjectKey": "FHIR",
      "Description": "Issue description...",
      "Summary": "Brief summary",
      "Type": "Bug",
      "TypeId": 1,
      "Priority": "Major",
      "PriorityId": 3,
      "Status": "Open",
      "StatusId": 1,
      "Resolution": "Unresolved",
      "ResolutionId": -1,
      "Assignee": "john.doe",
      "Reporter": "jane.smith",
      "CreatedAt": "2023-01-15T10:30:00Z",
      "UpdatedAt": "2023-02-01T14:20:00Z",
      "ResolvedAt": null,
      "WorkGroup": "FHIR-I",
      "Specification": "FHIR Core",
      "Vote": "For",
      "Grouping": "Normative"
    }
  ]
}
```

### Response Fields

#### Metadata
- `total`: Total number of issues matching the filters
- `returned`: Number of issues in current response
- `offset`: Current pagination offset
- `limit`: Maximum results per page
- `hasMore`: Boolean indicating if more results are available

#### Issue Fields
Each issue object contains all available JIRA fields including:
- Basic info: `Id`, `Key`, `Title`, `IssueUrl`, `Description`, `Summary`
- Project: `ProjectId`, `ProjectKey`
- Classification: `Type`, `TypeId`, `Priority`, `PriorityId`
- Status: `Status`, `StatusId`, `Resolution`, `ResolutionId`
- People: `Assignee`, `Reporter`
- Dates: `CreatedAt`, `UpdatedAt`, `ResolvedAt`, `VoteDate`
- FHIR-specific: `WorkGroup`, `Specification`, `Vote`, `Grouping`
- Additional: `Watches`, `AppliedForVersion`, `ChangeCategory`, etc.

## Usage Examples

### Basic Filtering
```json
{
  "project": "FHIR",
  "status": "Open",
  "limit": 25
}
```

### Complex Query with Multiple Filters
```json
{
  "project": "FHIR",
  "workgroup": "FHIR-I",
  "status": "Open",
  "priority": "Major",
  "assignee": "john.doe",
  "sort": "updated",
  "order": "desc",
  "limit": 50,
  "offset": 0
}
```

### Date Range Query
```json
{
  "project": "FHIR",
  "created_after": "2023-01-01",
  "created_before": "2023-12-31",
  "updated_after": "2023-06-01",
  "sort": "created",
  "order": "asc"
}
```

### Pagination Example
```json
{
  "project": "FHIR",
  "limit": 100,
  "offset": 200
}
```

## Input Validation

- `limit`: Must be greater than 0 and cannot exceed 1000
- `offset`: Cannot be negative
- Date parameters: Must be valid ISO 8601 format
- `order`: Must be either "asc" or "desc" (case insensitive)
- `sort`: Must be one of: "id", "key", "created", "updated", "priority"

## Error Responses

The tool returns error responses for invalid input:

```json
{
  "error": "Limit must be greater than 0"
}
```

```json
{
  "error": "Invalid date format for parameter 'created_after'. Expected ISO 8601 format (e.g., '2023-12-01T10:30:00Z' or '2023-12-01')"
}
```

## Performance Notes

- Date range filtering uses custom SQL queries for optimal performance
- Basic filtering uses generated ORM methods
- Large result sets are automatically paginated
- Sorting is performed at the database level
- Total count is calculated efficiently with separate COUNT query