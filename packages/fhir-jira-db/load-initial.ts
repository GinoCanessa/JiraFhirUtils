import { Database } from "bun:sqlite";
import { XMLParser } from "fast-xml-parser";
import path from "path";
import fs from "fs";
import { promises as fsPromises } from "fs";
import { getDatabasePath, setupDatabaseCliArgs } from "@jira-fhir-utils/database-utils";

// --- Configuration ---
const INITIAL_SUBDIRECTORY = "bulk"; // The subdirectory containing the initial XML files
const XML_GLOB_PATTERN = "**/*.xml";

// --- Type Definitions ---

interface XmlNode {
  "#text"?: string;
  "@_id"?: string;
  "@_key"?: string;
  "@_username"?: string;
  "@_author"?: string;
  "@_created"?: string;
  "@_colorName"?: string;
  [key: string]: any;
}

interface CustomFieldValue {
  "#text"?: string;
  [key: string]: any;
}

interface CustomField {
  "@_id"?: string;
  "@_key"?: string;
  customfieldname?: string;
  customfieldvalues?: {
    customfieldvalue?: CustomFieldValue | CustomFieldValue[];
  };
}

interface Comment {
  "@_id"?: string;
  "@_author"?: string;
  "@_created"?: string;
  "#text"?: string;
}

interface JiraItem {
  key?: XmlNode;
  title?: string;
  link?: string;
  project?: XmlNode;
  description?: string;
  summary?: string;
  type?: XmlNode;
  priority?: XmlNode;
  status?: {
    "#text"?: string;
    "@_id"?: string;
    statusCategory?: XmlNode;
  };
  resolution?: XmlNode;
  assignee?: XmlNode;
  reporter?: XmlNode;
  created?: string;
  updated?: string;
  resolved?: string;
  watches?: string | number;
  customfields?: {
    customfield?: CustomField[];
  };
  comments?: {
    comment?: Comment[];
  };
}

interface XmlData {
  rss?: {
    channel?: {
      item?: JiraItem[];
    };
  };
}

interface IssueRecord {
  key: string;
  id: string | undefined;
  title: string | undefined;
  link: string | undefined;
  project_id: string | undefined;
  project_key: string | undefined;
  description: string | undefined;
  summary: string | undefined;
  type: string | undefined;
  type_id: string | undefined;
  priority: string | undefined;
  priority_id: string | undefined;
  status: string | undefined;
  status_id: string | undefined;
  status_category_id: string | undefined;
  status_category_key: string | undefined;
  status_category_color: string | undefined;
  resolution: string | undefined;
  resolution_id: string | undefined;
  assignee: string | undefined;
  reporter: string | undefined;
  created_at: string | null;
  updated_at: string | null;
  resolved_at: string | null;
  watches: number;
  specification: string | null;
  appliedForVersion: string | null;
  changeCategory: string | null;
  changeImpact: string | null;
  duplicateIssue: string | null;
  grouping: string | null;
  raisedInVersion: string | null;
  relatedIssues: string | null;
  relatedArtifacts: string | null;
  relatedPages: string | null;
  relatedSections: string | null;
  relatedURL: string | null;
  resolutionDescription: string | null;
  voteDate: string | null;
  vote: string | null;
  workGroup: string | null;
}

interface CustomFieldRecord {
  issue_key: string;
  field_id: string | undefined;
  field_key: string | undefined;
  field_name: string | undefined;
  field_value: string | null;
}

interface CommentRecord {
  comment_id: string | undefined;
  issue_key: string;
  author: string | undefined;
  created_at: string | null;
  body: string | undefined;
}

interface CommandOptions {
  initialDir?: string;
  [key: string]: any;
}

interface CustomFieldDefinition {
  field_id: string;
  field_key: string;
  field_name: string;
  db_column: string;
}

// --- Custom field mapping ---
const dbFieldToCustomFieldName: Record<string, CustomFieldDefinition> = {
  specification: {
    field_id: "customfield_11302",
    field_key: "com.valiantys.jira.plugins.SQLFeed:nfeed-standard-customfield-type",
    field_name: "Specification",
    db_column: "specification"
  },
  appliedForVersion: {
    field_id: "customfield_11807",
    field_key: "com.valiantys.jira.plugins.SQLFeed:nfeed-standard-customfield-type",
    field_name: "Applied for version",
    db_column: "appliedForVersion"
  },
  changeCategory: {
    field_id: "customfield_10512",
    field_key: "com.atlassian.jira.plugin.system.customfieldtypes:select",
    field_name: "Change Category",
    db_column: "changeCategory"
  },
  changeImpact: {
    field_id: "customfield_10511",
    field_key: "com.atlassian.jira.plugin.system.customfieldtypes:select",
    field_name: "Change Impact",
    db_column: "changeImpact"
  },
  duplicateIssue: {
    field_id: "customfield_14909",
    field_key: "com.onresolve.jira.groovy.groovyrunner:single-issue-picker-cf",
    field_name: "Duplicate Issue",
    db_column: "duplicateIssue"
  },
  grouping: {
    field_id: "customfield_11402",
    field_key: "com.atlassian.jira.plugin.system.customfieldtypes:labels",
    field_name: "Grouping",
    db_column: "grouping"
  },
  raisedInVersion: {
    field_id: "customfield_11808",
    field_key: "com.valiantys.jira.plugins.SQLFeed:nfeed-standard-customfield-type",
    field_name: "Raised in version",
    db_column: "raisedInVersion"
  },
  relatedIssues: {
    field_id: "customfield_14905",
    field_key: "com.onresolve.jira.groovy.groovyrunner:multiple-issue-picker-cf",
    field_name: "Related Issues",
    db_column: "relatedIssues"
  },
  relatedArtifacts: {
    field_id: "customfield_11300",
    field_key: "com.valiantys.jira.plugins.SQLFeed:nfeed-standard-customfield-type",
    field_name: "Related Artifact(s)",
    db_column: "relatedArtifacts"
  },
  relatedPages: {
    field_id: "customfield_11301",
    field_key: "com.valiantys.jira.plugins.SQLFeed:nfeed-standard-customfield-type",
    field_name: "Related Page(s)",
    db_column: "relatedPages"
  },
  relatedSections: {
    field_id: "customfield_10518",
    field_key: "com.atlassian.jira.plugin.system.customfieldtypes:textfield",
    field_name: "Related Section(s)",
    db_column: "relatedSections"
  },
  relatedURL: {
    field_id: "customfield_10612",
    field_key: "com.atlassian.jira.plugin.system.customfieldtypes:url",
    field_name: "Related URL",
    db_column: "relatedURL"
  },
  resolutionDescription: {
    field_id: "customfield_10618",
    field_key: "com.atlassian.jira.plugin.system.customfieldtypes:textarea",
    field_name: "Resolution Description",
    db_column: "resolutionDescription"
  },
  voteDate: {
    field_id: "customfield_10525",
    field_key: "com.atlassian.jira.plugin.system.customfieldtypes:datepicker",
    field_name: "Vote Date",
    db_column: "voteDate"
  },
  vote: {
    field_id: "customfield_10510",
    field_key: "com.atlassian.jira.plugin.system.customfieldtypes:textfield",
    field_name: "Resolution Vote",
    db_column: "vote"
  },
  workGroup: {
    field_id: "customfield_11400",
    field_key: "com.valiantys.jira.plugins.SQLFeed:nfeed-standard-customfield-type",
    field_name: "Work Group",
    db_column: "workGroup"
  },
};

// --- Database Setup ---

/**
 * Initializes the SQLite database and creates the necessary tables.
 * @param db - The database instance.
 */
function setupDatabase(db: Database): void {
  console.log(`Initializing database...`);
  db.exec("PRAGMA journal_mode = WAL;"); // for better performance and concurrency

  // Main table for JIRA issues
  db.exec(`
    CREATE TABLE IF NOT EXISTS issues (
      key TEXT PRIMARY KEY,
      id INTEGER,
      title TEXT,
      link TEXT,
      project_id INTEGER,
      project_key TEXT,
      description TEXT,
      summary TEXT,
      type TEXT,
      type_id INTEGER,
      priority TEXT,
      priority_id INTEGER,
      status TEXT,
      status_id INTEGER,
      status_category_id INTEGER,
      status_category_key TEXT,
      status_category_color TEXT,
      resolution TEXT,
      resolution_id INTEGER,
      assignee TEXT,
      reporter TEXT,
      created_at TEXT,
      updated_at TEXT,
      resolved_at TEXT,
      watches INTEGER,
      specification TEXT,
      appliedForVersion TEXT,
      changeCategory TEXT,
      changeImpact TEXT,
      duplicateIssue TEXT,
      grouping TEXT,
      raisedInVersion TEXT,
      relatedIssues TEXT,
      relatedArtifacts TEXT,
      relatedPages TEXT,
      relatedSections TEXT,
      relatedURL TEXT,
      resolutionDescription TEXT,
      voteDate TEXT,
      vote TEXT,
      workGroup TEXT
    );
  `);

  // Table for custom fields (one-to-many relationship with issues)
  db.exec(`
    CREATE TABLE IF NOT EXISTS custom_fields (
      id INTEGER PRIMARY KEY AUTOINCREMENT,
      issue_key TEXT,
      field_id TEXT,
      field_key TEXT,
      field_name TEXT,
      field_value TEXT,
      FOREIGN KEY (issue_key) REFERENCES issues(key)
    );
  `);

    // Table for comments (one-to-many relationship with issues)
  db.exec(`
    CREATE TABLE IF NOT EXISTS comments (
      id INTEGER PRIMARY KEY AUTOINCREMENT,
      comment_id TEXT UNIQUE,
      issue_key TEXT,
      author TEXT,
      created_at TEXT,
      body TEXT,
      FOREIGN KEY (issue_key) REFERENCES issues(key)
    );
  `);

  createCommentsIndexes(db);
  
  console.log("Database schema is ready.");
}

/**
 * Creates indexes for the comments table to optimize query performance.
 * @param db - The database instance.
 */
function createCommentsIndexes(db: Database): void {
  console.log("Creating comments table indexes...");
  
  // Critical index for JOIN performance in loadIssues query
  db.exec("CREATE INDEX IF NOT EXISTS idx_comments_issue_key ON comments(issue_key)");
  console.log("✓ Created index: idx_comments_issue_key");
  
  // Covering index for GROUP_CONCAT optimization
  db.exec("CREATE INDEX IF NOT EXISTS idx_comments_issue_key_body ON comments(issue_key, body)");
  console.log("✓ Created index: idx_comments_issue_key_body");
  
  console.log("Comments indexes created successfully.");
}


/**
 * Parses a date string and returns it in ISO 8601 format.
 * Returns null if the date is invalid or not provided.
 * @param dateString - The date string to parse
 * @returns ISO 8601 formatted date string or null
 */
function toISO(dateString: string | undefined): string | null {
    if (!dateString) return null;
    const date = new Date(dateString);
    return isNaN(date.getTime()) ? null : date.toISOString();
}


/**
 * Processes a single XML file and loads its data into the database.
 * @param filePath - The path to the XML file.
 * @param db - The database instance.
 */
async function processXmlFile(filePath: string, db: Database): Promise<void> {
  console.log(`\nProcessing file: ${filePath}`);

  try {
    const fileContent = await fsPromises.readFile(filePath, 'utf-8');
    const parser = new XMLParser({
      ignoreAttributes: false,
      attributeNamePrefix: "@_",
      textNodeName: "#text",
      // Handle cases where a tag can be single or an array
      isArray: (name: string, jpath: string, isLeafNode: boolean, isAttribute: boolean): boolean => {
          return jpath === "rss.channel.item" ||
                 jpath === "rss.channel.item.customfields.customfield" ||
                 jpath === "rss.channel.item.comments.comment";
      }
    });
    const data = parser.parse(fileContent) as XmlData;

    const items = data?.rss?.channel?.item;
    if (!items || items.length === 0) {
      console.log(`No items found in ${filePath}.`);
      return;
    }

    // Prepare insert statements for performance
    const insertIssue = db.prepare(
        `INSERT OR IGNORE INTO issues (
          key, id, title, link, 
          project_id, project_key, description, summary, 
          type, type_id, priority, priority_id, 
          status, status_id, status_category_id, status_category_key, 
          status_category_color, resolution, resolution_id, assignee, 
          reporter, created_at, updated_at, resolved_at, 
          watches, specification, appliedForVersion, changeCategory, 
          changeImpact, duplicateIssue, grouping, raisedInVersion, 
          relatedIssues, relatedArtifacts, relatedPages, relatedSections, 
          relatedURL, resolutionDescription, voteDate, vote, 
          workGroup
         )
         VALUES (
          $key, $id, $title, $link, 
          $project_id, $project_key, $description, $summary, 
          $type, $type_id, $priority, $priority_id, 
          $status, $status_id, $status_category_id, $status_category_key, 
          $status_category_color, $resolution, $resolution_id, $assignee, 
          $reporter, $created_at, $updated_at, $resolved_at, 
          $watches, $specification, $appliedForVersion, $changeCategory,
          $changeImpact, $duplicateIssue, $grouping, $raisedInVersion,
          $relatedIssues, $relatedArtifacts, $relatedPages, $relatedSections,
          $relatedURL, $resolutionDescription, $voteDate, $vote,
          $workGroup
         )`
    );

    const insertCustomField = db.prepare(
        `INSERT INTO custom_fields (issue_key, field_id, field_key, field_name, field_value)
         VALUES ($issue_key, $field_id, $field_key, $field_name, $field_value)`
    );

     const insertComment = db.prepare(
        `INSERT OR IGNORE INTO comments (comment_id, issue_key, author, created_at, body)
         VALUES ($comment_id, $issue_key, $author, $created_at, $body)`
    );

    // Use a transaction for bulk inserts from a single file
    const insertAll = db.transaction((items: JiraItem[]) => {
        for (const item of items) {
            const issueKey = item?.key?.["#text"];
            if (!issueKey || typeof issueKey !== "string"){
                console.warn(`Skipping item with missing key in ${filePath}: ${item.title || "Unknown Title"}`);
                continue;
            }

            // insert the issue record with the properties we can get from the primary record - custom fields just set to null for now
            const issueRecord: IssueRecord = {
                key: issueKey,
                id: item.key?.["@_id"],
                title: item.title,
                link: item.link,
                project_id: item.project?.["@_id"],
                project_key: item.project?.["@_key"],
                description: item.description,
                summary: item.summary,
                type: item.type?.["#text"],
                type_id: item.type?.["@_id"],
                priority: item.priority?.["#text"],
                priority_id: item.priority?.["@_id"],
                status: item.status?.["#text"],
                status_id: item.status?.["@_id"],
                status_category_id: item.status?.statusCategory?.["@_id"],
                status_category_key: item.status?.statusCategory?.["@_key"],
                status_category_color: item.status?.statusCategory?.["@_colorName"],
                resolution: item.resolution?.["#text"],
                resolution_id: item.resolution?.["@_id"],
                assignee: item.assignee?.["@_username"],
                reporter: item.reporter?.["@_username"],
                created_at: toISO(item.created),
                updated_at: toISO(item.updated),
                resolved_at: toISO(item.resolved),
                watches: Number(item.watches) || 0,
                specification: null,
                appliedForVersion: null,
                changeCategory: null,
                changeImpact: null,
                duplicateIssue: null,
                grouping: null,
                raisedInVersion: null,
                relatedIssues: null,
                relatedArtifacts: null,
                relatedPages: null,
                relatedSections: null,
                relatedURL: null,
                resolutionDescription: null,
                voteDate: null,
                vote: null,
                workGroup: null,
            };

            insertIssue.run(issueRecord);

            // Process custom fields
            const customFields = item.customfields?.customfield;
            if (customFields) {
                for (const field of customFields) {
                    const valueNode = field.customfieldvalues?.customfieldvalue;
                    let value: string | null = null;
                    if (typeof valueNode === 'object' && valueNode !== null) {
                        if (Array.isArray(valueNode)) {
                            // Handle array of values
                            value = valueNode.map(v => v['#text'] || JSON.stringify(v)).join(', ');
                        } else {
                            value = valueNode['#text'] || JSON.stringify(valueNode);
                        }
                    } else if (valueNode !== undefined) {
                        value = String(valueNode);
                    }

                    const customFieldRecord: CustomFieldRecord = {
                        issue_key: issueKey,
                        field_id: field["@_id"],
                        field_key: field["@_key"],
                        field_name: field.customfieldname,
                        field_value: value,
                    };

                    insertCustomField.run(customFieldRecord);
                }
            }

            // Process comments
            const comments = item.comments?.comment;
            if (comments) {
                for(const comment of comments) {
                    const commentRecord: CommentRecord = {
                        comment_id: comment["@_id"],
                        issue_key: issueKey,
                        author: comment["@_author"],
                        created_at: toISO(comment["@_created"]),
                        body: comment["#text"],
                    };

                    insertComment.run(commentRecord);
                }
            }
        }
        return items.length;
    });

    const count = insertAll(items);
    console.log(`Successfully inserted or updated ${count} issues from ${filePath}.`);

  } catch (error) {
    console.error(`Failed to process file ${filePath}:`, error);
  }
}

async function updateIssuesFromCustomFields(db: Database): Promise<void> {
  console.log("\nMigrating select custom fields to main issues table...");

  let totalUpdated = 0;
  const startTime = Date.now();

  const basicSelect = `field_value`;
  const cleanedSelect = `COALESCE(trim(REPLACE(REPLACE(REPLACE(field_value, CHAR(10), ''), CHAR(13), ''), '&amp;', '&')), '')`;

  // Iterate through each custom field mapping
  for (const [dbColumn, fieldDef] of Object.entries(dbFieldToCustomFieldName)) {
    try {
      console.log(`Updating column '${dbColumn}' from custom field '${fieldDef.field_name}' (${fieldDef.field_id})...`);

      let selectExpression;
      switch (dbColumn) {
        case 'workGroup':
        case 'relatedArtifacts':
        case 'relatedPages':
          selectExpression = cleanedSelect;
          break;

        default:
          selectExpression = basicSelect;
          break;
      }

      // Prepare the UPDATE statement to transfer data from custom_fields to issues
      const updateQuery = `
        UPDATE issues 
        SET ${dbColumn} = (
          SELECT ${selectExpression} 
          FROM custom_fields 
          WHERE custom_fields.issue_key = issues.key 
          AND custom_fields.field_id = $field_id
        )
        WHERE EXISTS (
          SELECT 1 FROM custom_fields 
          WHERE custom_fields.issue_key = issues.key 
          AND custom_fields.field_id = $field_id
        )
      `;

      const updateStmt = db.prepare(updateQuery);
      const result = updateStmt.run({ field_id: fieldDef.field_id });
      
      console.log(`✓ Updated ${result.changes} records for '${dbColumn}'`);
      totalUpdated += result.changes;

    } catch (error) {
      console.error(`✗ Failed to update column '${dbColumn}':`, error);
      // Continue with other fields even if one fails
    }
  }

  const elapsedTime = Date.now() - startTime;
  console.log(`Migration completed: ${totalUpdated} total records updated in ${elapsedTime}ms`);
}


// --- Main Execution ---
async function main(): Promise<void> {
  console.log("Starting JIRA XML to SQLite import process...");

  // Setup CLI arguments
  const options = await setupDatabaseCliArgs('load-initial', 'Load initial JIRA XML files into SQLite database', {
    '--initial-dir <dir>': {
      description: 'Directory containing initial XML files',
      defaultValue: INITIAL_SUBDIRECTORY
    }
  }) as CommandOptions;

  let databasePath: string;
  try {
    databasePath = await getDatabasePath();
  } catch (error) {
    databasePath = path.join(process.cwd(), 'jira_issues.sqlite').replace(/\\/g, '/');
  }
  
  const initialDir = options.initialDir || INITIAL_SUBDIRECTORY;
  
  console.log(`Using database: ${databasePath}`);
  console.log(`Initial directory: ${initialDir}`);

  const db = new Database(databasePath, { strict: true, });
  setupDatabase(db);

  // Guard: Check if initial directory exists
  const initialPath = path.join(process.cwd(), initialDir).replace(/\\/g, '/');
  if (!fs.existsSync(initialPath)) {
    console.error(`Error: Initial content directory '${initialDir}' not found.`);
    return;
  }

  const glob = new Bun.Glob(XML_GLOB_PATTERN);
  const files = await Array.fromAsync(glob.scan({ cwd: initialPath, onlyFiles: true }))
    .then(results => results.map(file => path.join(initialPath, file)));

  if (files.length === 0) {
    console.log(`No XML files found matching pattern '${XML_GLOB_PATTERN}' in subdirectory '${initialDir}'.`);
    db.close();
    return;
  }

  console.log(`Found ${files.length} XML files to process.`);

  for (const filePath of files) {
    await processXmlFile(filePath.replace(/\\/g, '/'), db);
  }

  // Migrate custom field data from custom_fields table to issues table
  await updateIssuesFromCustomFields(db);

  db.close();
  console.log("\nImport process finished.");
}

main();