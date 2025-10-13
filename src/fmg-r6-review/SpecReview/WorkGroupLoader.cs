using JiraFhirUtils.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace fmg_r6_review.SpecReview;

public class WorkGroupLoader
{
    private CliConfig _config;

    public WorkGroupLoader(CliConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));

        if (string.IsNullOrEmpty(_config.DbPath))
        {
            throw new ArgumentException("DbPath must be provided.");
        }
    }

    public void LoadWorkGroups()
    {
        Console.WriteLine("Loading work groups...");
        Console.WriteLine($"Using database: {_config.DbPath}");

        using Microsoft.Data.Sqlite.SqliteConnection db = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_config.DbPath}");
        db.Open();

        // drop table first
        WorkGroupInfoRecord.DropTable(db);

        // create table
        WorkGroupInfoRecord.CreateTable(db);

        List<WorkGroupInfoRecord> wgs = [];

        // iterate over known work groups and insert them into the database
        foreach ((string code, string title) in FhirCommon.WorkgroupNames)
        {
            if (!FhirCommon.WorkgroupUrls.TryGetValue(code, out string? url))
            {
                throw new InvalidOperationException($"Workgroup code '{code}' does not have a corresponding URL.");
            }

            _ = FhirCommon.WorkgroupReplacement.TryGetValue(code, out string? replacedBy);

            wgs.Add(new WorkGroupInfoRecord
            {
                Code = code,
                Title = title,
                OfficialUrl = url,
                ReplacedBy = replacedBy,
            });
        }

        wgs.Insert(db, ignoreDuplicates: false, insertPrimaryKey: true);

        Console.WriteLine($"Loaded {wgs.Count} work group records.");
    }
}
