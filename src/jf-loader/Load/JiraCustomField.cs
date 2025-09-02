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
    public record class CustomFieldInfo
    {
        public required string FieldId { get; init; }
        public required string FieldKey { get; init; }
        public required string FieldName { get; init; }
        public required string DbColumn { get; init; }
    }

    /// <summary>
    /// Custom field mapping configuration - matches TypeScript version
    /// </summary>
    internal static readonly Dictionary<string, CustomFieldInfo> DbFieldToCustomFieldName = new()
    {
        ["specification"] = new()
        {
            FieldId = "customfield_11302",
            FieldKey = "com.valiantys.jira.plugins.SQLFeed:nfeed-standard-customfield-type",
            FieldName = "Specification",
            DbColumn = "specification"
        },
        ["appliedForVersion"] = new()
        {
            FieldId = "customfield_11807",
            FieldKey = "com.valiantys.jira.plugins.SQLFeed:nfeed-standard-customfield-type",
            FieldName = "Applied for version",
            DbColumn = "appliedForVersion"
        },
        ["changeCategory"] = new()
        {
            FieldId = "customfield_10512",
            FieldKey = "com.atlassian.jira.plugin.system.customfieldtypes:select",
            FieldName = "Change Category",
            DbColumn = "changeCategory"
        },
        ["changeImpact"] = new()
        {
            FieldId = "customfield_10511",
            FieldKey = "com.atlassian.jira.plugin.system.customfieldtypes:select",
            FieldName = "Change Impact",
            DbColumn = "changeImpact"
        },
        ["duplicateIssue"] = new()
        {
            FieldId = "customfield_14909",
            FieldKey = "com.onresolve.jira.groovy.groovyrunner:single-issue-picker-cf",
            FieldName = "Duplicate Issue",
            DbColumn = "duplicateIssue"
        },
        ["grouping"] = new()
        {
            FieldId = "customfield_11402",
            FieldKey = "com.atlassian.jira.plugin.system.customfieldtypes:labels",
            FieldName = "Grouping",
            DbColumn = "grouping"
        },
        ["raisedInVersion"] = new()
        {
            FieldId = "customfield_11808",
            FieldKey = "com.valiantys.jira.plugins.SQLFeed:nfeed-standard-customfield-type",
            FieldName = "Raised in version",
            DbColumn = "raisedInVersion"
        },
        ["relatedIssues"] = new()
        {
            FieldId = "customfield_14905",
            FieldKey = "com.onresolve.jira.groovy.groovyrunner:multiple-issue-picker-cf",
            FieldName = "Related Issues",
            DbColumn = "relatedIssues"
        },
        ["relatedArtifacts"] = new()
        {
            FieldId = "customfield_11300",
            FieldKey = "com.valiantys.jira.plugins.SQLFeed:nfeed-standard-customfield-type",
            FieldName = "Related Artifact(s)",
            DbColumn = "relatedArtifacts"
        },
        ["relatedPages"] = new()
        {
            FieldId = "customfield_11301",
            FieldKey = "com.valiantys.jira.plugins.SQLFeed:nfeed-standard-customfield-type",
            FieldName = "Related Page(s)",
            DbColumn = "relatedPages"
        },
        ["relatedSections"] = new()
        {
            FieldId = "customfield_10518",
            FieldKey = "com.atlassian.jira.plugin.system.customfieldtypes:textfield",
            FieldName = "Related Section(s)",
            DbColumn = "relatedSections"
        },
        ["relatedURL"] = new()
        {
            FieldId = "customfield_10612",
            FieldKey = "com.atlassian.jira.plugin.system.customfieldtypes:url",
            FieldName = "Related URL",
            DbColumn = "relatedURL"
        },
        ["resolutionDescription"] = new()
        {
            FieldId = "customfield_10618",
            FieldKey = "com.atlassian.jira.plugin.system.customfieldtypes:textarea",
            FieldName = "Resolution Description",
            DbColumn = "resolutionDescription"
        },
        ["voteDate"] = new()
        {
            FieldId = "customfield_10525",
            FieldKey = "com.atlassian.jira.plugin.system.customfieldtypes:datepicker",
            FieldName = "Vote Date",
            DbColumn = "voteDate"
        },
        ["vote"] = new()
        {
            FieldId = "customfield_10510",
            FieldKey = "com.atlassian.jira.plugin.system.customfieldtypes:textfield",
            FieldName = "Resolution Vote",
            DbColumn = "vote"
        },
        ["workGroup"] = new()
        {
            FieldId = "customfield_11400",
            FieldKey = "com.valiantys.jira.plugins.SQLFeed:nfeed-standard-customfield-type",
            FieldName = "Work Group",
            DbColumn = "workGroup"
        }
    };

}
