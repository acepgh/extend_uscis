namespace ExtendSchemaGenerator;

/// <summary>
/// Catalog of USCIS and EOIR forms with download URLs.
/// Extracted from TUF Questionnaires sheet.
/// </summary>
public static class FormCatalog
{
    public record FormInfo(string FormNumber, string DisplayName, FormSource Source, string? Notes = null);
    
    public enum FormSource
    {
        USCIS,  // https://www.uscis.gov/sites/default/files/document/forms/{form}.pdf
        EOIR,   // https://www.justice.gov/eoir/file/{id}/download
        Other
    }

    /// <summary>
    /// All forms from the TUF Questionnaire tracker.
    /// </summary>
    public static readonly FormInfo[] AllForms = new[]
    {
        // USCIS I-Forms
        new FormInfo("I-90", "Application to Replace Permanent Resident Card", FormSource.USCIS),
        new FormInfo("I-102", "Application for Replacement/Initial Nonimmigrant Arrival-Departure Record", FormSource.USCIS),
        new FormInfo("I-129F", "Petition for Alien Fiancé(e)", FormSource.USCIS),
        new FormInfo("I-130", "Petition for Alien Relative", FormSource.USCIS),
        new FormInfo("I-130A", "Supplemental Information for Spouse Beneficiary", FormSource.USCIS),
        new FormInfo("I-131", "Application for Travel Document", FormSource.USCIS),
        new FormInfo("I-131A", "Application for Travel Document (Carrier Documentation)", FormSource.USCIS),
        new FormInfo("I-192", "Application for Advance Permission to Enter as Nonimmigrant", FormSource.USCIS),
        new FormInfo("I-212", "Application for Permission to Reapply for Admission", FormSource.USCIS),
        new FormInfo("I-246", "Application for Stay of Deportation or Removal", FormSource.USCIS),
        new FormInfo("I-290B", "Notice of Appeal or Motion", FormSource.USCIS),
        new FormInfo("I-360", "Petition for Amerasian, Widow(er), or Special Immigrant", FormSource.USCIS),
        new FormInfo("I-485", "Application to Register Permanent Residence or Adjust Status", FormSource.USCIS),
        new FormInfo("I-539", "Application to Extend/Change Nonimmigrant Status", FormSource.USCIS),
        new FormInfo("I-589", "Application for Asylum and for Withholding of Removal", FormSource.USCIS),
        new FormInfo("I-601", "Application for Waiver of Grounds of Inadmissibility", FormSource.USCIS),
        new FormInfo("I-601A", "Application for Provisional Unlawful Presence Waiver", FormSource.USCIS),
        new FormInfo("I-639", "Application for Waiver (Ineligibility Based on Health)", FormSource.USCIS),
        new FormInfo("I-730", "Refugee/Asylee Relative Petition", FormSource.USCIS),
        new FormInfo("I-751", "Petition to Remove Conditions on Residence", FormSource.USCIS),
        new FormInfo("I-765", "Application for Employment Authorization", FormSource.USCIS),
        new FormInfo("I-765WS", "I-765 Worksheet", FormSource.USCIS),
        new FormInfo("I-821", "Application for Temporary Protected Status", FormSource.USCIS),
        new FormInfo("I-821D", "Consideration of Deferred Action for Childhood Arrivals", FormSource.USCIS),
        new FormInfo("I-824", "Application for Action on an Approved Application or Petition", FormSource.USCIS),
        new FormInfo("I-864", "Affidavit of Support Under Section 213A of the INA", FormSource.USCIS),
        new FormInfo("I-881", "Application for Suspension of Deportation or Special Rule Cancellation", FormSource.USCIS),
        new FormInfo("I-912", "Request for Fee Waiver", FormSource.USCIS),
        new FormInfo("I-914", "Application for T Nonimmigrant Status", FormSource.USCIS),
        new FormInfo("I-914A", "Supplement A to Form I-914", FormSource.USCIS, "Also known as I-914 Supplement A"),
        new FormInfo("I-918", "Petition for U Nonimmigrant Status", FormSource.USCIS),
        new FormInfo("I-918A", "Supplement A to Form I-918", FormSource.USCIS, "Petition for Qualifying Family Member"),
        new FormInfo("I-918B", "Supplement B to Form I-918", FormSource.USCIS, "U Nonimmigrant Status Certification"),

        // USCIS N-Forms
        new FormInfo("N-336", "Request for Hearing on Decision in Naturalization Proceedings", FormSource.USCIS),
        new FormInfo("N-400", "Application for Naturalization", FormSource.USCIS),
        new FormInfo("N-565", "Application for Replacement Naturalization/Citizenship Document", FormSource.USCIS),
        new FormInfo("N-600", "Application for Certificate of Citizenship", FormSource.USCIS),
        new FormInfo("N-648", "Medical Certification for Disability Exceptions", FormSource.USCIS),

        // USCIS G-Forms
        new FormInfo("G-28", "Notice of Entry of Appearance as Attorney or Accredited Representative", FormSource.USCIS),
        new FormInfo("G-325A", "Biographic Information", FormSource.USCIS),

        // USCIS AR-Forms
        new FormInfo("AR-11", "Alien's Change of Address Card", FormSource.USCIS),

        // EOIR Forms (Department of Justice)
        new FormInfo("EOIR-26", "Notice of Appeal from a Decision of an Immigration Judge", FormSource.EOIR),
        new FormInfo("EOIR-27", "Notice of Entry of Appearance as Attorney or Representative Before the BIA", FormSource.EOIR),
        new FormInfo("EOIR-28", "Notice of Entry of Appearance as Attorney or Representative Before the Immigration Court", FormSource.EOIR),
        new FormInfo("EOIR-33", "Change of Address Form/Immigration Court", FormSource.EOIR),
        new FormInfo("EOIR-42A", "Application for Cancellation of Removal for Certain Permanent Residents", FormSource.EOIR),
        new FormInfo("EOIR-42B", "Application for Cancellation of Removal for Certain Nonpermanent Residents", FormSource.EOIR),
        new FormInfo("EOIR-59", "Request for Telephonic/Video Conference Appearance", FormSource.EOIR),
        new FormInfo("EOIR-60", "Application for Temporary Admission", FormSource.EOIR),
        new FormInfo("EOIR-61", "Motion to Reopen/Reconsider", FormSource.EOIR),
    };

    /// <summary>
    /// Get the download URL for a form.
    /// </summary>
    public static string GetDownloadUrl(FormInfo form)
    {
        return form.Source switch
        {
            FormSource.USCIS => GetUscisUrl(form.FormNumber),
            FormSource.EOIR => GetEoirUrl(form.FormNumber),
            _ => throw new NotSupportedException($"No URL pattern for source: {form.Source}")
        };
    }

    /// <summary>
    /// Get the instructions URL for a USCIS form.
    /// </summary>
    public static string? GetInstructionsUrl(FormInfo form)
    {
        if (form.Source != FormSource.USCIS)
            return null;
            
        var formLower = form.FormNumber.ToLowerInvariant();
        return $"https://www.uscis.gov/sites/default/files/document/forms/{formLower}instr.pdf";
    }

    private static string GetUscisUrl(string formNumber)
    {
        var formLower = formNumber.ToLowerInvariant();
        return $"https://www.uscis.gov/sites/default/files/document/forms/{formLower}.pdf";
    }

    private static string GetEoirUrl(string formNumber)
    {
        // EOIR forms use a different URL pattern
        var formLower = formNumber.ToLowerInvariant().Replace("-", "");
        return $"https://www.justice.gov/eoir/file/{formLower}/download";
    }

    /// <summary>
    /// Find a form by number (case-insensitive).
    /// </summary>
    public static FormInfo? FindForm(string formNumber)
    {
        return AllForms.FirstOrDefault(f => 
            f.FormNumber.Equals(formNumber, StringComparison.OrdinalIgnoreCase));
    }
}
