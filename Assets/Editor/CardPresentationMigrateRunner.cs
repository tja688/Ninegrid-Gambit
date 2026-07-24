#if UNITY_EDITOR
using NineGrid.Content.Editor;
using UnityEditor;
using UnityEngine;

/// <summary>One-shot migration invoker for Pipeline eval (no string literals with spaces).</summary>
public static class CardPresentationMigrateRunner
{
    [MenuItem("NineGrid/Tools/Migrate Card Presentation JSON")]
    public static void Run()
    {
        var session = new CardPresentationEditorSession();
        session.Reload();
        var created = session.MigrateFromLegacyCatalogs(out var message);
        Debug.Log("[CardPresentationMigrate] created=" + created + " " + message);
        if (session.TryExportIndex(out var indexMsg))
        {
            Debug.Log("[CardPresentationMigrate] " + indexMsg);
        }
    }
}
#endif
