/**
 * Authoritative database record interfaces for FHIR JIRA utilities
 * 
 * These interfaces define the structure of database records used throughout
 * the application for type safety and consistency.
 */

/**
 * Represents a JIRA issue record in the database
 */
export interface IssueRecord {
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

/**
 * Represents a custom field record associated with a JIRA issue
 */
export interface CustomFieldRecord {
  issue_key: string;
  field_id: string | undefined;
  field_key: string | undefined;
  field_name: string | undefined;
  field_value: string | null;
}

/**
 * Represents a comment record on a JIRA issue
 */
export interface CommentRecord {
  comment_id: string | undefined;
  issue_key: string;
  author: string | undefined;
  created_at: string | null;
  body: string | undefined;
}

/**
 * Represents a keyword record with TF-IDF scoring information
 */
export interface KeywordRecord {
  keyword: string;
  tfidf_score: number;
  tf_score: number;
  idf_score: number;
}