#!/usr/bin/env bun

import { Database } from "bun:sqlite";
import { getDatabasePath, setupDatabaseCliArgs } from "@jira-fhir-utils/database-utils";

interface IssuesFTSRow {
    issue_key: string;
    issue_int: number;
    title: string;
    description: string;
    summary: string;
    resolution_description: string;
}

interface CommentsFTSRow {
    comment_id: string;
    issue_key: string;
    author: string;
    body: string;
}

interface CountResult {
    count: number;
}

function setupFTS5Tables(db: Database): void {
    console.log("Dropping and creating FTS5 tables...");
    
    db.exec(`DROP table IF EXISTS issues_fts`);

    db.exec(`
        CREATE VIRTUAL TABLE IF NOT EXISTS issues_fts USING fts5(
            issue_key,
            issue_int,
            title,
            description,
            summary,
            resolution_description
        )
    `);

    db.exec(`DROP table IF EXISTS comments_fts`);

    db.exec(`
        CREATE VIRTUAL TABLE IF NOT EXISTS comments_fts USING fts5(
            comment_id,
            issue_key,
            author,
            body,
            content=comments,
            content_rowid=id
        )
    `);
    
    console.log("FTS5 tables created successfully");
}

function populateIssuesFTS(db: Database): void {
    console.log("Populating issues_fts table...");
    
    const startTime = Date.now();
    
    db.exec("BEGIN TRANSACTION");
    
    try {
        db.exec(`
            INSERT INTO issues_fts (
                issue_key,
                issue_int,
                title,
                description,
                summary,
                resolution_description
            )
            SELECT 
                i.key,
                CAST(SUBSTR(i.key, INSTR(i.key, '-') + 1) AS INTEGER),
                i.title,
                i.description,
                i.summary,
                i.resolution_description
            FROM issues i
        `);
        
        db.exec("COMMIT");
        
        const result = db.prepare("SELECT COUNT(*) as count FROM issues_fts").get() as CountResult;
        const count = result.count;
        const elapsed = Date.now() - startTime;
        console.log(`✓ Populated issues_fts with ${count} entries in ${elapsed}ms`);
    } catch (error) {
        db.exec("ROLLBACK");
        throw error;
    }
}

function populateCommentsFTS(db: Database): void {
    console.log("Populating comments_fts table...");
    
    const startTime = Date.now();
    
    db.exec("BEGIN TRANSACTION");
    
    try {
        db.exec(`
            INSERT INTO comments_fts (
                comment_id,
                issue_key,
                author,
                body
            )
            SELECT 
                comment_id,
                issue_key,
                author,
                body
            FROM comments
        `);
        
        db.exec("COMMIT");
        
        const result = db.prepare("SELECT COUNT(*) as count FROM comments_fts").get() as CountResult;
        const count = result.count;
        const elapsed = Date.now() - startTime;
        console.log(`✓ Populated comments_fts with ${count} entries in ${elapsed}ms`);
    } catch (error) {
        db.exec("ROLLBACK");
        throw error;
    }
}

async function main(): Promise<void> {
    console.log("Creating FTS5 tables for JIRA issues...\n");
    
    // Setup CLI arguments
    const options = await setupDatabaseCliArgs('create-fts', 'Create FTS5 search tables for JIRA issues');
    
    const databasePath = await getDatabasePath();
    console.log(`Using database: ${databasePath}`);
    
    if (!(await Bun.file(databasePath).exists())) {
        console.error(`Error: Database file '${databasePath}' not found.`);
        console.error("Please run load-initial.js first to create the database.");
        process.exit(1);
    }
    
    const db = new Database(databasePath, { strict: true });
    
    try {
        db.exec("PRAGMA journal_mode = WAL;");
        
        setupFTS5Tables(db);
        
        populateIssuesFTS(db);
        
        populateCommentsFTS(db);
        
        console.log("\n✓ FTS5 setup completed successfully!");
        console.log("\nExample queries you can now run:");
        console.log("- Search issues: SELECT * FROM issues_fts WHERE issues_fts MATCH 'search term'");
        console.log("- Search comments: SELECT * FROM comments_fts WHERE comments_fts MATCH 'search term'");
        console.log("- Phrase search: SELECT * FROM issues_fts WHERE issues_fts MATCH '\"exact phrase\"'");
        console.log("- Field-specific: SELECT * FROM issues_fts WHERE title MATCH 'search term'");
        
    } catch (error) {
        console.error("Error setting up FTS5:", error);
        process.exit(1);
    } finally {
        db.close();
    }
}

main().catch(console.error);