using System;
using System.Collections.Generic;
using System.Text;

namespace JiraFhirUtils.SQLiteGenerator;

public class GeneratorAttributes
{
    internal const string _jfSQLiteBaseClass =  "JfSQLiteBaseClass";
    internal const string _jfSQLiteTable = "JfSQLiteTable";
    internal const string _jfSQLiteIndex =  "JfSQLiteIndex";
    internal const string _jfSQLiteKey =  "JfSQLiteKey";
    internal const string _jfSQLiteForeignKey =  "JfSQLiteForeignKey";
    internal const string _jfSQLiteIgnore = "JfSQLiteIgnore";
    internal const string _jfSQLiteUnique = "JfSQLiteUnique";

    internal static HashSet<string> _jfAttributes = [
        _jfSQLiteBaseClass,
        _jfSQLiteTable,
        _jfSQLiteIndex,
        _jfSQLiteKey,
        _jfSQLiteForeignKey,
        _jfSQLiteIgnore,
        _jfSQLiteUnique,
        ];

    internal static HashSet<string> _jfClassAttributes = [
        _jfSQLiteBaseClass,
        _jfSQLiteTable,
        ];


    internal const string JfAttributes = $$$"""
        #nullable enable
        namespace JiraFhirUtils.SQLiteGenerator
        {
            [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
            public class {{{_jfSQLiteBaseClass}}} : System.Attribute
            {
                public {{{_jfSQLiteBaseClass}}}()
                {
                }
            }

            [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
            public class {{{_jfSQLiteTable}}} : System.Attribute
            {
                public string? TableName { get; set; }
                public bool DynamicTableNames { get; set; }

                public {{{_jfSQLiteTable}}}(string? tableName = null, bool dynamicTableNames = false)
                {
                    TableName = tableName;
                    DynamicTableNames = dynamicTableNames;
                }
            }

            [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
            public class {{{_jfSQLiteIndex}}} : System.Attribute
            {
                public string[] Columns { get; set; }
        
                public {{{_jfSQLiteIndex }}}(params string[] columns)
                {
                    Columns = columns;
                }
            }
        
            [System.AttributeUsage(System.AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
            public class {{{_jfSQLiteKey}}} : System.Attribute
            {
                public bool AutoIncrement { get; set; }
                public {{{_jfSQLiteKey}}}(bool autoIncrement = true)
                {
                    AutoIncrement = autoIncrement;
                }
            }

            [System.AttributeUsage(System.AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
            public class {{{_jfSQLiteForeignKey}}} : System.Attribute
            {
                public string? ReferenceTable { get; set; }
                public string? ReferenceColumn { get; set; }
                public {{{_jfSQLiteForeignKey}}}(string? referenceTable = null, string? referenceColumn = null)
                {
                    ReferenceTable = referenceTable;
                    ReferenceColumn = referenceColumn;
                }
            }

            [System.AttributeUsage(System.AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
            public class {{{_jfSQLiteIgnore}}} : System.Attribute
            {
                public {{{_jfSQLiteIgnore}}}()
                {
                }
            }

            [System.AttributeUsage(System.AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
            public class {{{_jfSQLiteUnique}}} : System.Attribute
            {
                public {{{_jfSQLiteUnique}}}()
                {
                }
            }
                }
        #nullable restore
        """;
}
