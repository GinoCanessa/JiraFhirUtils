using JiraFhirUtils.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace jf_loader.Load;

internal class JiraCustomField
{
    /// <summary>
    /// Represents the mapping configuration for a JIRA custom field.
    /// </summary>
    public record class CustomFieldMappingInfo
    {
        public required string FieldId { get; init; }
        public required string FieldKey { get; init; }
        public required string FieldName { get; init; }
        public required string DbColumn { get; init; }
        public bool UseCoalesce { get; init; } = false;
    }

    /// <summary>
    /// Custom field mapping configuration - matches TypeScript version
    /// </summary>
    internal static readonly List<CustomFieldMappingInfo> CustomFieldMappings = [
        new()
        {
            FieldId = "customfield_11302",
            FieldKey = "com.valiantys.jira.plugins.SQLFeed:nfeed-standard-customfield-type",
            FieldName = "Specification",
            DbColumn = nameof(IssueRecord.Specification),
        },
        new()
        {
            FieldId = "customfield_11807",
            FieldKey = "com.valiantys.jira.plugins.SQLFeed:nfeed-standard-customfield-type",
            FieldName = "Applied for version",
            DbColumn = nameof(IssueRecord.AppliedForVersion),
        },
        new()
        {
            FieldId = "customfield_10512",
            FieldKey = "com.atlassian.jira.plugin.system.customfieldtypes:select",
            FieldName = "Change Category",
            DbColumn = nameof(IssueRecord.ChangeCategory),
        },
        new()
        {
            FieldId = "customfield_10511",
            FieldKey = "com.atlassian.jira.plugin.system.customfieldtypes:select",
            FieldName = "Change Impact",
            DbColumn = nameof(IssueRecord.ChangeImpact),
        },
        new()
        {
            FieldId = "customfield_14909",
            FieldKey = "com.onresolve.jira.groovy.groovyrunner:single-issue-picker-cf",
            FieldName = "Duplicate Issue",
            DbColumn = nameof(IssueRecord.DuplicateIssue),
        },
        new()
        {
            FieldId = "customfield_14907",
            FieldKey = "com.onresolve.jira.groovy.groovyrunner:single-issue-picker-cf",
            FieldName = "Duplicate Voted Issue",
            DbColumn = nameof(IssueRecord.DuplicateVotedIssue),
        },
        new()
        {
            FieldId = "customfield_11402",
            FieldKey = "com.atlassian.jira.plugin.system.customfieldtypes:labels",
            FieldName = "Grouping",
            DbColumn = nameof(IssueRecord.Grouping),
        },
        new()
        {
            FieldId = "customfield_11808",
            FieldKey = "com.valiantys.jira.plugins.SQLFeed:nfeed-standard-customfield-type",
            FieldName = "Raised in version",
            DbColumn = nameof(IssueRecord.RaisedInVersion),
        },
        new()
        {
            FieldId = "customfield_14905",
            FieldKey = "com.onresolve.jira.groovy.groovyrunner:multiple-issue-picker-cf",
            FieldName = "Related Issues",
            DbColumn = nameof(IssueRecord.RelatedIssues),
        },
        new()
        {
            FieldId = "customfield_11300",
            FieldKey = "com.valiantys.jira.plugins.SQLFeed:nfeed-standard-customfield-type",
            FieldName = "Related Artifact(s)",
            DbColumn = nameof(IssueRecord.RelatedArtifacts),
            UseCoalesce = true,
        },
        new()
        {
            FieldId = "customfield_11301",
            FieldKey = "com.valiantys.jira.plugins.SQLFeed:nfeed-standard-customfield-type",
            FieldName = "Related Page(s)",
            DbColumn = nameof(IssueRecord.RelatedPages),
            UseCoalesce = true,
        },
        new()
        {
            FieldId = "customfield_10518",
            FieldKey = "com.atlassian.jira.plugin.system.customfieldtypes:textfield",
            FieldName = "Related Section(s)",
            DbColumn = nameof(IssueRecord.RelatedSections),
        },
        new()
        {
            FieldId = "customfield_10612",
            FieldKey = "com.atlassian.jira.plugin.system.customfieldtypes:url",
            FieldName = "Related URL",
            DbColumn = nameof(IssueRecord.RelatedUrl),
        },
        new()
        {
            FieldId = "customfield_10618",
            FieldKey = "com.atlassian.jira.plugin.system.customfieldtypes:textarea",
            FieldName = "Resolution Description",
            DbColumn = nameof(IssueRecord.ResolutionDescription),
        },
         new()
        {
            FieldId = "customfield_10525",
            FieldKey = "com.atlassian.jira.plugin.system.customfieldtypes:datepicker",
            FieldName = "Vote Date",
            DbColumn = nameof(IssueRecord.VoteDate),
        },
        new()
        {
            FieldId = "customfield_10510",
            FieldKey = "com.atlassian.jira.plugin.system.customfieldtypes:textfield",
            FieldName = "Resolution Vote",
            DbColumn = nameof(IssueRecord.Vote),
        },
        new()
        {
            FieldId = "customfield_11400",
            FieldKey = "com.valiantys.jira.plugins.SQLFeed:nfeed-standard-customfield-type",
            FieldName = "Work Group",
            DbColumn = nameof(IssueRecord.WorkGroup),
            UseCoalesce = true,
        },
        new()
        {
            FieldId = "customfield_14904",
            FieldKey = "com.onresolve.jira.groovy.groovyrunner:scripted-field",
            FieldName = "Block Vote",
            DbColumn = nameof(IssueRecord.BlockVote),
        },
        new()
        {
            FieldId = "customfield_10902",
            FieldKey = "com.valiantys.jira.plugins.SQLFeed:nfeed-standard-customfield-type",
            FieldName = "Selected Ballot",
            DbColumn = nameof(IssueRecord.SelectedBallot),
            UseCoalesce = true,
        },
        new()
        {
            FieldId = "customfield_11000",
            FieldKey = "com.atlassian.jira.plugin.system.customfieldtypes:multiuserpicker",
            FieldName = "Request in-person",
            DbColumn = nameof(IssueRecord.RequestInPerson),
            UseCoalesce = true,
        },
    ];

}
