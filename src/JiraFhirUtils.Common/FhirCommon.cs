using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JiraFhirUtils.Common;

public static class FhirCommon
{

    public static string ResolveWorkgroup(string? value, string defaultValue = "fhir")
    {
        if (string.IsNullOrEmpty(value))
        {
            return defaultValue;
        }

        if (WorkgroupReplacement.TryGetValue(value!, out string? replacement))
        {
            return replacement;
        }

        if (WorkgroupUrls.ContainsKey(value!))
        {
            return value!;
        }

        return defaultValue;
    }

    /// <summary>
    /// Resolve disbanded (but listed) HL7 Workgroup names to their replacements.
    /// </summary>
    public static readonly Dictionary<string, string> WorkgroupReplacement = new(StringComparer.OrdinalIgnoreCase)
    {
        { "rcrim", "brr" },
        { "mnm", "fhir" },
        { "ti", "vocab" },
    };

    /// <summary>
    /// Resolve an HL7 Workgroup name to its URL.
    /// </summary>
    /// <remarks>
    /// Pulled from:
    ///     - https://github.com/hapifhir/org.hl7.fhir.core/blob/master/org.hl7.fhir.utilities/src/main/java/org/hl7/fhir/utilities/HL7WorkGroups.java
    ///     - on 2025.08.18
    /// </remarks>
    public static readonly Dictionary<string, string> WorkgroupUrls = new(StringComparer.OrdinalIgnoreCase)
    {
        { "aid", "http://www.hl7.org/Special/committees/java" },
        { "arden", "http://www.hl7.org/Special/committees/arden" },
        { "brr", "http://www.hl7.org/Special/committees/rcrim" },
        { "cbcc", "http://www.hl7.org/Special/committees/cbcc" },
        { "cdamg", "http://www.hl7.org/Special/committees/cdamg" },
        { "cds", "http://www.hl7.org/Special/committees/dss" },
        { "cg", "http://www.hl7.org/Special/committees/clingenomics" },
        { "cgp", "http://www.hl7.org/Special/committees/cgp" },
        { "cic", "http://www.hl7.org/Special/committees/cic" },
        { "cimi", "http://www.hl7.org/Special/committees/cimi" },
        { "claims", "http://www.hl7.org/Special/committees/claims" },
        { "cqi", "http://www.hl7.org/Special/committees/cqi" },
        { "dev", "http://www.hl7.org/Special/committees/healthcaredevices" },
        { "ehr", "http://www.hl7.org/Special/committees/ehr" },
        { "ec", "http://www.hl7.org/Special/committees/emergencycare" },
        { "fhir", "http://www.hl7.org/Special/committees/fiwg" },
        { "fmg", "http://www.hl7.org/Special/committees/fhirmg" },
        { "fm", "http://www.hl7.org/Special/committees/fm" },
        { "hsi", "http://www.hl7.org/Special/committees/hsi" },
        { "hsswg", "http://www.hl7.org/Special/committees/hsswg" },
        { "hta", "http://www.hl7.org/Special/committees/termauth" },
        { "ictc", "http://www.hl7.org/Special/committees/ictc" },
        { "ii", "http://www.hl7.org/Special/committees/imagemgt" },
        { "inm", "http://www.hl7.org/Special/committees/inm" },
        { "its", "http://www.hl7.org/Special/committees/xml" },
        { "lhs", "http://www.hl7.org/Special/committees/lhs" },
        { "mnm", "http://www.hl7.org/Special/committees/mnm" },
        { "mobile", "http://www.hl7.org/Special/committees/mobile" },
        { "oo", "http://www.hl7.org/Special/committees/orders" },
        { "pa", "http://www.hl7.org/Special/committees/pafm" },
        { "pe", "http://www.hl7.org/Special/committees/patientempowerment" },
        { "pc", "http://www.hl7.org/Special/committees/patientcare" },
        { "pher", "http://www.hl7.org/Special/committees/pher" },
        { "phx", "http://www.hl7.org/Special/committees/medication" },
        { "sd", "http://www.hl7.org/Special/committees/structure" },
        { "sec", "http://www.hl7.org/Special/committees/secure" },
        { "soa", "http://www.hl7.org/Special/committees/soa" },
        { "ti", "http://www.hl7.org/Special/committees/Vocab" },
        { "tsmg", "http://www.hl7.org/Special/committees/tsmg" },
        { "us", "http://www.hl7.org/Special/committees/usrealm" },
        { "v2", "http://www.hl7.org/Special/committees/v2management" },
        {  "vocab", "http://www.hl7.org/Special/committees/Vocab" },
    };

    /// <summary>
    /// Resolve an HL7 Workgroup name to its human-readable name.
    /// </summary>
    /// <remarks>
    /// Pulled from:
    ///     - https://github.com/hapifhir/org.hl7.fhir.core/blob/master/org.hl7.fhir.utilities/src/main/java/org/hl7/fhir/utilities/HL7WorkGroups.java
    ///     - on 2025.08.18
    /// </remarks>
    public static readonly Dictionary<string, string> WorkgroupNames = new(StringComparer.OrdinalIgnoreCase)
    {
        { "aid", "Application Implementation and Design" },
        { "arden", "Arden Syntax" },
        { "brr", "Biomedical Research and Regulation" },
        { "cbcc", "Community Based Collaborative Care" },
        { "cdamg", "CDA Management Group" },
        { "cds", "Clinical Decision Support" },
        { "cg", "Clinical Genomics" },
        { "cgp", "Cross-Group Projects" },
        { "cic", "Clinical Interoperability Council" },
        { "cimi", "Clinical Information Modeling Initiative" },
        { "claims", "Payer/Provider Information Exchange Work Group" },
        { "cqi", "Clinical Quality Information" },
        { "dev", "Health Care Devices" },
        { "ehr", "Electronic Health Records" },
        { "ec", "Emergency Care" },
        { "fhir", "FHIR Infrastructure" },
        { "fmg", "FHIR Management Group" },
        { "fm", "Financial Management" },
        { "hsi", "Health Standards Integration" },
        { "hsswg", "Human and Social Services" },
        { "hta", "Terminology Authority" },
        { "ictc", "Conformance" },
        { "ii", "Imaging Integration" },
        { "inm", "Infrastructure And Messaging" },
        { "its", "Implementable Technology Specifications" },
        { "lhs", "Learning Health Systems" },
        { "mnm", "Modeling and Methodology" },
        { "mobile", "Mobile Health" },
        { "oo", "Orders and Observations" },
        { "pa", "Patient Administration" },
        { "pe", "Patient Empowerment" },
        { "pc", "Patient Care" },
        { "pher", "Public Health" },
        { "phx", "Pharmacy" },
        { "sd", "Structured Documents" },
        { "sec", "Security" },
        { "soa", "Services Oriented Architecture" },
        { "ti", "Terminology Infrastructure" },
        { "tsmg", "Terminology Services Management Group (TSMG)" },
        { "us", "US Realm Steering Committee" },
        { "v2", "V2 Management Group" },
        { "vocab", "HL7 International / Terminology Infrastructure" },
        //{ "vocab", "Terminology Infrastructure" },
    };
}
