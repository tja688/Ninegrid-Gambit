using NineGrid.Content.Editor;

public static class ContentArtBreakLinkReport
{
    public static string FirstFailure()
    {
        var findings = ContentArtBreakLinkValidator.ValidateAuthoringCards();
        if (findings.Count == 0)
        {
            return "ok count=0";
        }

        var x = findings[0];
        return "count=" + findings.Count
               + " | " + x.ContentId
               + " | " + x.Field
               + " | " + x.Kind
               + " | " + x.Path
               + " | " + x.Reason;
    }
}
