/// <summary>
/// Central list of the tag strings the game looks up at runtime.
///
/// Several systems find each other with GameObject.FindWithTag rather than an
/// Inspector reference, because Inspector references are lost when a
/// DontDestroyOnLoad object survives a scene change. That makes these strings
/// part of the API - a typo fails at runtime instead of at compile time.
/// Referencing them through this class means the compiler catches the typo.
///
/// If you add a tag here, add it in Edit > Project Settings > Tags and Layers too.
/// </summary>
public static class GameTags
{
    /// <summary>The persistent GameOrganizer object.</summary>
    public const string Organizer = "Organizer";

    /// <summary>
    /// Goes on BOTH bot roots - including the AI bot. The hitbox scripts test
    /// for this tag, so a bot tagged anything else is invulnerable.
    /// </summary>
    public const string Bot = "Player";

    /// <summary>Player 1's lives counter (TextMeshProUGUI).</summary>
    public const string Scoring1 = "Scoring1";

    /// <summary>Player 2's lives counter (TextMeshProUGUI).</summary>
    public const string Scoring2 = "Scoring2";

    /// <summary>The AI's lives counter (TextMeshProUGUI).</summary>
    public const string ScoringAI = "ScoringAI";
}
