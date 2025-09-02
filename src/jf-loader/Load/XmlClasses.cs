using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace jf_loader.Load
{
    /// <summary>
    /// Root RSS element for JIRA XML export files
    /// </summary>
    [XmlRoot("rss")]
    public class JiraRss
    {
        [XmlAttribute("version")]
        public string Version { get; set; } = string.Empty;

        [XmlElement("channel")]
        public JiraChannel Channel { get; set; } = new();
    }

    /// <summary>
    /// RSS channel containing JIRA issues
    /// </summary>
    public class JiraChannel
    {
        [XmlElement("title")]
        public string Title { get; set; } = string.Empty;

        [XmlElement("link")]
        public string Link { get; set; } = string.Empty;

        [XmlElement("description")]
        public string Description { get; set; } = string.Empty;

        [XmlElement("language")]
        public string Language { get; set; } = string.Empty;

        [XmlElement("issue")]
        public JiraIssueInfo IssueInfo { get; set; } = new();

        [XmlElement("build-info")]
        public JiraBuildInfo BuildInfo { get; set; } = new();

        [XmlElement("item")]
        public List<JiraItem> Items { get; set; } = new();
    }

    /// <summary>
    /// Issue information metadata
    /// </summary>
    public class JiraIssueInfo
    {
        [XmlAttribute("start")]
        public int Start { get; set; }

        [XmlAttribute("end")]
        public int End { get; set; }

        [XmlAttribute("total")]
        public int Total { get; set; }
    }

    /// <summary>
    /// Build information for JIRA instance
    /// </summary>
    public class JiraBuildInfo
    {
        [XmlElement("version")]
        public string Version { get; set; } = string.Empty;

        [XmlElement("build-number")]
        public string BuildNumber { get; set; } = string.Empty;

        [XmlElement("build-date")]
        public string BuildDate { get; set; } = string.Empty;
    }

    /// <summary>
    /// Individual JIRA issue item
    /// </summary>
    public class JiraItem
    {
        [XmlElement("title")]
        public string Title { get; set; } = string.Empty;

        [XmlElement("link")]
        public string Link { get; set; } = string.Empty;

        [XmlElement("project")]
        public JiraProject Project { get; set; } = new();

        [XmlElement("description")]
        public string Description { get; set; } = string.Empty;

        [XmlElement("key")]
        public JiraKey Key { get; set; } = new();

        [XmlElement("summary")]
        public string Summary { get; set; } = string.Empty;

        [XmlElement("type")]
        public JiraType Type { get; set; } = new();

        [XmlElement("priority")]
        public JiraPriority Priority { get; set; } = new();

        [XmlElement("status")]
        public JiraStatus Status { get; set; } = new();

        [XmlElement("resolution")]
        public JiraResolution? Resolution { get; set; }

        [XmlElement("assignee")]
        public JiraUser? Assignee { get; set; }

        [XmlElement("reporter")]
        public JiraUser? Reporter { get; set; }

        [XmlElement("created")]
        public string Created { get; set; } = string.Empty;

        [XmlElement("updated")]
        public string Updated { get; set; } = string.Empty;

        [XmlElement("resolved")]
        public string? Resolved { get; set; }

        [XmlElement("watches")]
        public int Watches { get; set; }

        [XmlElement("comments")]
        public JiraComments? Comments { get; set; }

        [XmlElement("attachments")]
        public JiraAttachments? Attachments { get; set; }

        [XmlElement("subtasks")]
        public JiraSubtasks? Subtasks { get; set; }

        [XmlElement("customfields")]
        public JiraCustomFields? CustomFields { get; set; }
    }

    /// <summary>
    /// Project with ID and key attributes
    /// </summary>
    public class JiraProject
    {
        [XmlAttribute("id")]
        public int Id { get; set; }

        [XmlAttribute("key")]
        public string Key { get; set; } = string.Empty;

        [XmlText]
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// Issue key with ID attribute
    /// </summary>
    public class JiraKey
    {
        [XmlAttribute("id")]
        public int Id { get; set; }

        [XmlText]
        public string Value { get; set; } = string.Empty;
    }

    /// <summary>
    /// Issue type with ID and icon attributes
    /// </summary>
    public class JiraType
    {
        [XmlAttribute("id")]
        public int Id { get; set; }

        [XmlAttribute("iconUrl")]
        public string IconUrl { get; set; } = string.Empty;

        [XmlText]
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// Priority with ID and icon attributes
    /// </summary>
    public class JiraPriority
    {
        [XmlAttribute("id")]
        public int Id { get; set; }

        [XmlAttribute("iconUrl")]
        public string IconUrl { get; set; } = string.Empty;

        [XmlText]
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// Status with ID, icon, and description attributes, plus nested status category
    /// </summary>
    public class JiraStatus
    {
        [XmlAttribute("id")]
        public int Id { get; set; }

        [XmlAttribute("iconUrl")]
        public string IconUrl { get; set; } = string.Empty;

        [XmlAttribute("description")]
        public string Description { get; set; } = string.Empty;

        [XmlText]
        public string Name { get; set; } = string.Empty;

        [XmlElement("statusCategory")]
        public JiraStatusCategory? StatusCategory { get; set; }
    }

    /// <summary>
    /// Status category with ID, key, and color attributes
    /// </summary>
    public class JiraStatusCategory
    {
        [XmlAttribute("id")]
        public int Id { get; set; }

        [XmlAttribute("key")]
        public string Key { get; set; } = string.Empty;

        [XmlAttribute("colorName")]
        public string ColorName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Resolution with ID attribute
    /// </summary>
    public class JiraResolution
    {
        [XmlAttribute("id")]
        public int Id { get; set; }

        [XmlText]
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// User (assignee/reporter) with username attribute
    /// </summary>
    public class JiraUser
    {
        [XmlAttribute("username")]
        public string Username { get; set; } = string.Empty;

        [XmlText]
        public string DisplayName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Comments container
    /// </summary>
    public class JiraComments
    {
        [XmlElement("comment")]
        public List<JiraComment> CommentList { get; set; } = new();
    }

    /// <summary>
    /// Individual comment with ID, author, and created attributes
    /// </summary>
    public class JiraComment
    {
        [XmlAttribute("id")]
        public int Id { get; set; }

        [XmlAttribute("author")]
        public string Author { get; set; } = string.Empty;

        [XmlAttribute("created")]
        public string Created { get; set; } = string.Empty;

        [XmlText]
        public string Body { get; set; } = string.Empty;
    }

    /// <summary>
    /// Attachments container (placeholder)
    /// </summary>
    public class JiraAttachments
    {
        // Placeholder for attachment structure if needed
    }

    /// <summary>
    /// Subtasks container (placeholder)
    /// </summary>
    public class JiraSubtasks
    {
        // Placeholder for subtask structure if needed
    }

    /// <summary>
    /// Custom fields container
    /// </summary>
    public class JiraCustomFields
    {
        [XmlElement("customfield")]
        public List<JiraXmlCustomField> CustomFieldList { get; set; } = new();
    }

    /// <summary>
    /// Individual custom field with ID and key attributes (XML-specific class)
    /// </summary>
    public class JiraXmlCustomField
    {
        [XmlAttribute("id")]
        public string Id { get; set; } = string.Empty;

        [XmlAttribute("key")]
        public string Key { get; set; } = string.Empty;

        [XmlElement("customfieldname")]
        public string CustomFieldName { get; set; } = string.Empty;

        [XmlElement("customfieldvalues")]
        public JiraCustomFieldValues? CustomFieldValues { get; set; }
    }

    /// <summary>
    /// Custom field values container
    /// </summary>
    public class JiraCustomFieldValues
    {
        [XmlElement("customfieldvalue")]
        public List<JiraCustomFieldValue> Values { get; set; } = new();
    }

    /// <summary>
    /// Individual custom field value with optional key attribute
    /// </summary>
    public class JiraCustomFieldValue
    {
        [XmlAttribute("key")]
        public string Key { get; set; } = string.Empty;

        [XmlText]
        public string Value { get; set; } = string.Empty;
    }

    /// <summary>
    /// Helper class for XML deserialization operations
    /// </summary>
    public static class JiraXmlHelper
    {
        /// <summary>
        /// Deserializes a JIRA XML file into strongly-typed objects
        /// </summary>
        /// <param name="xmlFilePath">Path to the XML file</param>
        /// <returns>Deserialized JiraRss object containing all issues</returns>
        /// <exception cref="ArgumentNullException">Thrown when xmlFilePath is null or empty</exception>
        /// <exception cref="FileNotFoundException">Thrown when the XML file doesn't exist</exception>
        /// <exception cref="InvalidOperationException">Thrown when XML deserialization fails</exception>
        public static JiraRss DeserializeFromFile(string xmlFilePath)
        {
            if (string.IsNullOrWhiteSpace(xmlFilePath))
                throw new ArgumentNullException(nameof(xmlFilePath));

            if (!File.Exists(xmlFilePath))
                throw new FileNotFoundException($"XML file not found: {xmlFilePath}");

            var serializer = new XmlSerializer(typeof(JiraRss));
            
            using var fileStream = new FileStream(xmlFilePath, FileMode.Open, FileAccess.Read);
            using var reader = new StreamReader(fileStream, Encoding.UTF8);
            
            var result = serializer.Deserialize(reader) as JiraRss;
            return result ?? throw new InvalidOperationException("Failed to deserialize XML file");
        }

        /// <summary>
        /// Deserializes JIRA XML from a string
        /// </summary>
        /// <param name="xmlContent">XML content as string</param>
        /// <returns>Deserialized JiraRss object containing all issues</returns>
        /// <exception cref="ArgumentNullException">Thrown when xmlContent is null or empty</exception>
        /// <exception cref="InvalidOperationException">Thrown when XML deserialization fails</exception>
        public static JiraRss DeserializeFromString(string xmlContent)
        {
            if (string.IsNullOrWhiteSpace(xmlContent))
                throw new ArgumentNullException(nameof(xmlContent));

            var serializer = new XmlSerializer(typeof(JiraRss));
            
            using var reader = new StringReader(xmlContent);
            
            var result = serializer.Deserialize(reader) as JiraRss;
            return result ?? throw new InvalidOperationException("Failed to deserialize XML content");
        }

        /// <summary>
        /// Deserializes JIRA XML from a stream
        /// </summary>
        /// <param name="xmlStream">XML stream</param>
        /// <returns>Deserialized JiraRss object containing all issues</returns>
        /// <exception cref="ArgumentNullException">Thrown when xmlStream is null</exception>
        /// <exception cref="InvalidOperationException">Thrown when XML deserialization fails</exception>
        public static JiraRss DeserializeFromStream(Stream xmlStream)
        {
            if (xmlStream == null)
                throw new ArgumentNullException(nameof(xmlStream));

            var serializer = new XmlSerializer(typeof(JiraRss));
            
            var result = serializer.Deserialize(xmlStream) as JiraRss;
            return result ?? throw new InvalidOperationException("Failed to deserialize XML stream");
        }
    }
}
