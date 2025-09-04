using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JiraFhirUtils.SQLiteGenerator;

namespace JiraFhirUtils.Common.FhirDbModels;

[JfSQLiteBaseClass]
public abstract class CgDbBase
{
    [JfSQLiteKey]
    public int Key { get; set; } = -1;

    //internal static int _indexValue = 0;
    //public static int GetIndex() => Interlocked.Increment(ref _indexValue);
}
