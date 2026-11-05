/// <summary>
/// The player bot's spinner hitbox.
///
/// All behaviour lives in BotHitBox. This class exists only so the Player
/// prefab's existing component reference (and every Inspector field wired into
/// it) keeps working - Unity references scripts by GUID, and the GUID belongs to
/// the file name.
///
/// Tuning is per-prefab in the Inspector now, so if the player and the AI should
/// hit with different strength, set that here rather than by editing code.
/// </summary>
public class PlayerHitBox : BotHitBox
{
}
