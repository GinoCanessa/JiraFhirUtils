using JiraFhirUtils.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
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
        WorkgroupRecord.DropTable(db);

        // create table
        WorkgroupRecord.CreateTable(db);

        List<WorkgroupRecord> wgs = [];

        // iterate over known work groups and insert them into the database
        foreach ((string code, string title) in FhirCommon.WorkgroupNames)
        {
            if (!FhirCommon.WorkgroupUrls.TryGetValue(code, out string? url))
            {
                throw new InvalidOperationException($"Workgroup code '{code}' does not have a corresponding URL.");
            }

            _ = FhirCommon.WorkgroupReplacement.TryGetValue(code, out string? replacedBy);

            wgs.Add(new WorkgroupRecord
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

    public void LoadFmgFeedbackSheetContent()
    {
        // from https://docs.google.com/spreadsheets/d/1vWh8ms21HhyyN7ImuyBqoA0CC4s2Od4AS60ubZ0zDUo/edit?gid=0#gid=0
        // exported on October 17 at 9:50 AM US Central
        string sheetJson = """
                        [
                {
                    "Resource": "AdministrableProductDefinition",
                    "WG": "brr",
                    "FMG Recommendation": "AR Group 2",
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "ClinicalUseDefinition",
                    "WG": "brr",
                    "FMG Recommendation": "AR Group 2",
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "Ingredient",
                    "WG": "brr",
                    "FMG Recommendation": "AR Group 2",
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "ManufacturedItemDefinition",
                    "WG": "brr",
                    "FMG Recommendation": "AR Group 2",
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "MedicinalProductDefinition",
                    "WG": "brr",
                    "FMG Recommendation": "AR Group 2",
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "PackagedProductDefinition",
                    "WG": "brr",
                    "FMG Recommendation": "AR Group 2",
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "RegulatedAuthorization",
                    "WG": "brr",
                    "FMG Recommendation": "AR Group 2",
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "ResearchStudy",
                    "WG": "brr",
                    "FMG Recommendation": null,
                    "Track": "AR",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": "Lots of changes in R6 and probably beyond",
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "ResearchSubject",
                    "WG": "brr",
                    "FMG Recommendation": null,
                    "Track": "AR",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": "Lots of changes in R6 and probably beyond",
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "Substance",
                    "WG": "brr",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "SubstanceDefinition",
                    "WG": "brr",
                    "FMG Recommendation": "AR Group 2",
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "SubstanceNucleicAcid",
                    "WG": "brr",
                    "FMG Recommendation": "AR Group 2",
                    "Track": null,
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "SubstancePolymer",
                    "WG": "brr",
                    "FMG Recommendation": "AR Group 2",
                    "Track": null,
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "SubstanceProtein",
                    "WG": "brr",
                    "FMG Recommendation": "AR Group 2",
                    "Track": null,
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "SubstanceReferenceInformation",
                    "WG": "brr",
                    "FMG Recommendation": "AR Group 2",
                    "Track": null,
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "SubstanceSourceMaterial",
                    "WG": "brr",
                    "FMG Recommendation": null,
                    "Track": null,
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "Consent",
                    "WG": "cbcc",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "ActivityDefinition",
                    "WG": "cds",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": "Citation should be AR, all others Normative",
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "ArtifactAssessment",
                    "WG": "cds",
                    "FMG Recommendation": "AR Group 2",
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": "Citation should be AR, all others Normative",
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "Citation",
                    "WG": "cds",
                    "FMG Recommendation": "AR Group 2",
                    "Track": "AR",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": "Citation should be AR, all others Normative",
                    "Target": "EBM-on-FHIR IG",
                    "Unnamed: 9": null
                },
                {
                    "Resource": "DetectedIssue",
                    "WG": "cds",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": "Citation should be AR, all others Normative",
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "EventDefinition",
                    "WG": "cds",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": "Citation should be AR, all others Normative",
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "Evidence",
                    "WG": "cds",
                    "FMG Recommendation": "AR Group 2",
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": "Citation should be AR, all others Normative",
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "EvidenceVariable",
                    "WG": "cds",
                    "FMG Recommendation": "AR Group 2",
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": "Citation should be AR, all others Normative",
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "GuidanceResponse",
                    "WG": "cds",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": "Citation should be AR, all others Normative",
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "Library",
                    "WG": "cds",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": "Citation should be AR, all others Normative",
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "PlanDefinition",
                    "WG": "cds",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": "Citation should be AR, all others Normative",
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "RequestOrchestration",
                    "WG": "cds",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": "Citation should be AR, all others Normative",
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "RiskAssessment",
                    "WG": "cds",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": "Citation should be AR, all others Normative",
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "GenomicStudy",
                    "WG": "cg",
                    "FMG Recommendation": "AR Group 2",
                    "Track": "AR",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "MolecularDefinition",
                    "WG": "cg",
                    "FMG Recommendation": "AR Group 2",
                    "Track": "AR",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "MolecularSequence",
                    "WG": "cg",
                    "FMG Recommendation": "AR Group 2",
                    "Track": "AR",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "Measure",
                    "WG": "cqi",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": "Agreement on keeping Measure and MeasureReport",
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "MeasureReport",
                    "WG": "cqi",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": "Agreement on keeping Measure and MeasureReport",
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "DeviceAlert",
                    "WG": "dev",
                    "FMG Recommendation": "AR Group 2",
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": "2025-09-16 FHIR-i\/DEV Q2\nTodd Cooper\/Michael Faughn\n19-0-2",
                    "Notes": "2025-10-01: Confirmed by Devices WG (https:\/\/confluence.hl7.org\/spaces\/HCD\/pages\/358898432\/2025-10-01+Devices+Main+Call+Agenda+and+Minutes)",
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "DeviceMetric",
                    "WG": "dev",
                    "FMG Recommendation": "N",
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": "2025-09-16 FHIR-i\/DEV Q2\nTodd Cooper\/Michael Faughn\n19-0-2",
                    "Notes": "2025-10-01: Confirmed by Devices WG (https:\/\/confluence.hl7.org\/spaces\/HCD\/pages\/358898432\/2025-10-01+Devices+Main+Call+Agenda+and+Minutes)",
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "ActorDefinition",
                    "WG": "fhir",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "Basic",
                    "WG": "fhir",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "Binary",
                    "WG": "fhir",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "Bundle",
                    "WG": "fhir",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "CapabilityStatement",
                    "WG": "fhir",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "CompartmentDefinition",
                    "WG": "fhir",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "ExampleScenario",
                    "WG": "fhir",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "GraphDefinition",
                    "WG": "fhir",
                    "FMG Recommendation": null,
                    "Track": "AR",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": "NOTE: This resource is referenced in Formulary IG, but optional usage\nClinical Guidelines makes pretty extensive use of this resource to describe definitions of documents",
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "Group",
                    "WG": "fhir",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "ImplementationGuide",
                    "WG": "fhir",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "List",
                    "WG": "fhir",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "OperationDefinition",
                    "WG": "fhir",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "OperationOutcome",
                    "WG": "fhir",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "Parameters",
                    "WG": "fhir",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "Questionnaire",
                    "WG": "fhir",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "QuestionnaireResponse",
                    "WG": "fhir",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "Requirements",
                    "WG": "fhir",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "SearchParameter",
                    "WG": "fhir",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "StructureDefinition",
                    "WG": "fhir",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "StructureMap",
                    "WG": "fhir",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "Subscription",
                    "WG": "fhir",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "SubscriptionStatus",
                    "WG": "fhir",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "SubscriptionTopic",
                    "WG": "fhir",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "TestPlan",
                    "WG": "fhir",
                    "FMG Recommendation": "AR Group 2",
                    "Track": "AR",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "TestReport",
                    "WG": "fhir",
                    "FMG Recommendation": "AR Group 2",
                    "Track": "AR",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "TestScript",
                    "WG": "fhir",
                    "FMG Recommendation": null,
                    "Track": "AR",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "Claim",
                    "WG": "fm",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "ClaimResponse",
                    "WG": "fm",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "Contract",
                    "WG": "fm",
                    "FMG Recommendation": null,
                    "Track": "AR",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "Coverage",
                    "WG": "fm",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "CoverageEligibilityRequest",
                    "WG": "fm",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "CoverageEligibilityResponse",
                    "WG": "fm",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "EnrollmentRequest",
                    "WG": "fm",
                    "FMG Recommendation": "AR Group 2",
                    "Track": "AR",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "EnrollmentResponse",
                    "WG": "fm",
                    "FMG Recommendation": "AR Group 2",
                    "Track": "AR",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "ExplanationOfBenefit",
                    "WG": "fm",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "Invoice",
                    "WG": "fm",
                    "FMG Recommendation": null,
                    "Track": "Further consultation needed",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "PaymentNotice",
                    "WG": "fm",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "PaymentReconciliation",
                    "WG": "fm",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "ImagingSelection",
                    "WG": "ii",
                    "FMG Recommendation": "AR Group 2",
                    "Track": "N",
                    "VotedByWg": "X",
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": "IHE Radiology, FHIRCast, prefer to keep unless there are significant changes coming\n2025-09-30: Imaging Integration reviewed the spreadsheet and can confirm that the resources we manage are in the correct track \u2014 both ImagingStudy and ImagingSelection to go normative in R6.",
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "ImagingStudy",
                    "WG": "ii",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": "X",
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": "2025-09-30: Imaging Integration reviewed the spreadsheet and can confirm that the resources we manage are in the correct track \u2014 both ImagingStudy and ImagingSelection to go normative in R6.",
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "MessageDefinition",
                    "WG": "inm",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": "X",
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": "2025-09-30: InM voted this morning to keep MessageHeader and MessageDefinition in base FHIR for R6, such that they will go normative. We found the reference to the IGs that profile MessageDefinition useful in this decision.",
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "MessageHeader",
                    "WG": "inm",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": "X",
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": "2025-09-30: InM voted this morning to keep MessageHeader and MessageDefinition in base FHIR for R6, such that they will go normative. We found the reference to the IGs that profile MessageDefinition useful in this decision.",
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "BiologicallyDerivedProduct",
                    "WG": "oo",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "BiologicallyDerivedProductDispense",
                    "WG": "oo",
                    "FMG Recommendation": "AR Group 1",
                    "Track": "AR",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": "Preliminary, OO need to discuss more",
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "BodyStructure",
                    "WG": "oo",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "Device",
                    "WG": "oo",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "DeviceAssociation",
                    "WG": "oo",
                    "FMG Recommendation": "AR Group 1",
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": "DEV is in support of N, they wish it were stable - key piece for them, important connection.",
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "DeviceDefinition",
                    "WG": "oo",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "DeviceDispense",
                    "WG": "oo",
                    "FMG Recommendation": "AR Group 1",
                    "Track": "AR",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "DeviceRequest",
                    "WG": "oo",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "DeviceUsage",
                    "WG": "oo",
                    "FMG Recommendation": "AR Group 1",
                    "Track": "AR",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "DiagnosticReport",
                    "WG": "oo",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "DocumentReference",
                    "WG": "oo",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "InventoryItem",
                    "WG": "oo",
                    "FMG Recommendation": "AR Group 1",
                    "Track": "AR",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "InventoryReport",
                    "WG": "oo",
                    "FMG Recommendation": "AR Group 1",
                    "Track": "AR",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "NutritionIntake",
                    "WG": "oo",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "NutritionOrder",
                    "WG": "oo",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "NutritionProduct",
                    "WG": "oo",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "Observation",
                    "WG": "oo",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": "$operation stats will go AR, $lastn will stay",
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "ObservationDefinition",
                    "WG": "oo",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "ServiceRequest",
                    "WG": "oo",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "Specimen",
                    "WG": "oo",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "SpecimenDefinition",
                    "WG": "oo",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "SupplyDelivery",
                    "WG": "oo",
                    "FMG Recommendation": null,
                    "Track": "AR",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": "Want to move these to AR because they will evolve with Inentory, not mature enough",
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "SupplyRequest",
                    "WG": "oo",
                    "FMG Recommendation": null,
                    "Track": "AR",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": "Want to move these to AR because they will evolve with Inentory, not mature enough",
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "Task",
                    "WG": "oo",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "Transport",
                    "WG": "oo",
                    "FMG Recommendation": "AR Group 1",
                    "Track": "AR",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "VisionPrescription",
                    "WG": "oo",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "Account",
                    "WG": "pa",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": "https:\/\/confluence.hl7.org\/spaces\/PA\/pages\/51224251\/FHIR+R6+Plan",
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "Appointment",
                    "WG": "pa",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": "Remove $everything and $merge",
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "AppointmentResponse",
                    "WG": "pa",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "ChargeItem",
                    "WG": "pa",
                    "FMG Recommendation": null,
                    "Track": "AR",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "ChargeItemDefinition",
                    "WG": "pa",
                    "FMG Recommendation": null,
                    "Track": "AR",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "Encounter",
                    "WG": "pa",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "EncounterHistory",
                    "WG": "pa",
                    "FMG Recommendation": "AR Group 1",
                    "Track": "AR",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "Endpoint",
                    "WG": "pa",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "EpisodeOfCare",
                    "WG": "pa",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "HealthcareService",
                    "WG": "pa",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "InsurancePlan",
                    "WG": "pa",
                    "FMG Recommendation": null,
                    "Track": "AR",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": "Move to FM\nFormulary, PLan net, NDH (Not technical in reg, but it is a very visible US effort",
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "InsuranceProduct",
                    "WG": "pa",
                    "FMG Recommendation": "AR Group 1",
                    "Track": "AR",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": "Move to FM\nElements currently in R4 InsurancePlan used by the above IGs",
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "Location",
                    "WG": "pa",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "Organization",
                    "WG": "pa",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "OrganizationAffiliation",
                    "WG": "pa",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "Patient",
                    "WG": "pa",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "Person",
                    "WG": "pa",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "PersonalRelationship",
                    "WG": "pa",
                    "FMG Recommendation": "AR Group 1",
                    "Track": "AR",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "Practitioner",
                    "WG": "pa",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": "Additional Resources Assessment"
                },
                {
                    "Resource": "PractitionerRole",
                    "WG": "pa",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": "Trial Use Elements Assessment"
                },
                {
                    "Resource": "RelatedPerson",
                    "WG": "pa",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "Schedule",
                    "WG": "pa",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "Slot",
                    "WG": "pa",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "VerificationResult",
                    "WG": "pa",
                    "FMG Recommendation": null,
                    "Track": "AR",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "AdverseEvent",
                    "WG": "pc",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": "Differing opinions on Normative Resources (whether to keep in R6)\nGenerally, there is more harm\/consequences with removing resources vs possibly needing breaking changes on normative resources\nRuss Leftwich is most concerned that the decision is binary - either normative or nothing.  Resources may not have been fully implemented (implementers focused on elements in US Core profiles).  Lack of detailed tracking on which elements have been implemented (beyond just the IG). ",
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "AllergyIntolerance",
                    "WG": "pc",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": "Agreement for Patient Care to remove the Trial Use designation on all 4 elements (all have been implemented by large EHRs as well as having been available since DSTU2)\nAllergyIntolerance.type\nAllergyIntolerance.reaction",
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "CarePlan",
                    "WG": "pc",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "CareTeam",
                    "WG": "pc",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "ClinicalAssessment",
                    "WG": "pc",
                    "FMG Recommendation": "AR Group 2",
                    "Track": "AR",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": "Agreement on Additional Resources recommendation from FMG (remove from R6) \u2013 ConditionDefinition, ClinicalAssessment, and Linkage",
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "Communication",
                    "WG": "pc",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "CommunicationRequest",
                    "WG": "pc",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "Condition",
                    "WG": "pc",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": "Agreement for Patient Care to remove the Trial Use designation on all 4 elements (all have been implemented by large EHRs as well as having been available since DSTU2)\nCondition.stage\nCondition.evidence",
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "ConditionDefinition",
                    "WG": "pc",
                    "FMG Recommendation": "AR Group 1",
                    "Track": "AR",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": "Agreement on Additional Resources recommendation from FMG (remove from R6) \u2013 ConditionDefinition, ClinicalAssessment, and Linkage",
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "FamilyMemberHistory",
                    "WG": "pc",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "Flag",
                    "WG": "pc",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "Goal",
                    "WG": "pc",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "Linkage",
                    "WG": "pc",
                    "FMG Recommendation": "AR Group 2",
                    "Track": "AR",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": "Agreement on Additional Resources recommendation from FMG (remove from R6) \u2013 ConditionDefinition, ClinicalAssessment, and Linkage",
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "Procedure",
                    "WG": "pc",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "Immunization",
                    "WG": "ph",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": "X",
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": "Minutes: https:\/\/confluence.hl7.org\/spaces\/PHWG\/pages\/358283945\/2025-10-02+Public+Health+Work+Group+Call+Minutes",
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "ImmunizationEvaluation",
                    "WG": "ph",
                    "FMG Recommendation": null,
                    "Track": "AR",
                    "VotedByWg": "X",
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": "Minutes: https:\/\/confluence.hl7.org\/spaces\/PHWG\/pages\/358283945\/2025-10-02+Public+Health+Work+Group+Call+Minutes",
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "ImmunizationRecommendation",
                    "WG": "ph",
                    "FMG Recommendation": null,
                    "Track": "AR",
                    "VotedByWg": "X",
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": "Minutes: https:\/\/confluence.hl7.org\/spaces\/PHWG\/pages\/358283945\/2025-10-02+Public+Health+Work+Group+Call+Minutes",
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "FormularyItem",
                    "WG": "phx",
                    "FMG Recommendation": "AR Group 1",
                    "Track": "AR",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": "In Da Vinci Formulary as a Basic with plans to move to this resource. It is fairly primitive, so possibly ok to hold off",
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "Medication",
                    "WG": "phx",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "MedicationAdministration",
                    "WG": "phx",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "MedicationDispense",
                    "WG": "phx",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "MedicationKnowledge",
                    "WG": "phx",
                    "FMG Recommendation": null,
                    "Track": "AR",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": "Desire to deprecate this resource",
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "MedicationRequest",
                    "WG": "phx",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "MedicationStatement",
                    "WG": "phx",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "Composition",
                    "WG": "sd",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "AuditEvent",
                    "WG": "sec",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "Permission",
                    "WG": "sec",
                    "FMG Recommendation": "AR Group 1",
                    "Track": "AR",
                    "VotedByWg": null,
                    "WG to FMG": "x",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "Provenance",
                    "WG": "sec",
                    "FMG Recommendation": null,
                    "Track": "N",
                    "VotedByWg": null,
                    "WG to FMG": "X",
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "CodeSystem",
                    "WG": "vocab",
                    "FMG Recommendation": null,
                    "Track": null,
                    "VotedByWg": null,
                    "WG to FMG": null,
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "ConceptMap",
                    "WG": "vocab",
                    "FMG Recommendation": null,
                    "Track": null,
                    "VotedByWg": null,
                    "WG to FMG": null,
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "NamingSystem",
                    "WG": "vocab",
                    "FMG Recommendation": null,
                    "Track": null,
                    "VotedByWg": null,
                    "WG to FMG": null,
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "TerminologyCapabilities",
                    "WG": "vocab",
                    "FMG Recommendation": null,
                    "Track": null,
                    "VotedByWg": null,
                    "WG to FMG": null,
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                },
                {
                    "Resource": "ValueSet",
                    "WG": "vocab",
                    "FMG Recommendation": null,
                    "Track": null,
                    "VotedByWg": null,
                    "WG to FMG": null,
                    "Vote": null,
                    "Notes": null,
                    "Target": null,
                    "Unnamed: 9": null
                }
            ]
            """;

        List<FmgSheetContentRecord>? records = JsonSerializer.Deserialize<List<FmgSheetContentRecord>>(sheetJson);

        if (records is null)
        {
            return;
        }

        using Microsoft.Data.Sqlite.SqliteConnection db = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_config.DbPath}");
        db.Open();

        if (_config.DropTables)
        {
            FmgSheetContentRecord.DropTable(db);
        }

        FmgSheetContentRecord.CreateTable(db);

        records.Insert(db);
    }

}
