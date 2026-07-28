using NineGrid.Content.Editor;

/// <summary>Pipeline eval 入口：#66 ContentArt 迁移（无空格字符串）。</summary>
public static class ContentArtMigrateEval
{
    public static string Run()
    {
        return ContentArtMigrateRunner.Migrate();
    }

    public static int ValidateAuthoringFailureCount()
    {
        return ContentArtBreakLinkValidator.ValidateAuthoringCards().Count;
    }
}
