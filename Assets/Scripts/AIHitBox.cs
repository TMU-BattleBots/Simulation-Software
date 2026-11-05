/// <summary>
/// The AI bot's spinner hitbox.
///
/// All behaviour lives in BotHitBox. This class exists only so the AI Variant
/// prefab's existing component reference (and every Inspector field wired into
/// it) keeps working.
///
/// IMPORTANT - this is the biggest behavioural change in the bug-fix pass.
/// This script used to apply knockback with ForceMode2D.Force where the player's
/// used ForceMode2D.Impulse. Force spreads the push across one physics step,
/// so combined with the old Time.deltaTime factor the AI's knockback was in the
/// region of fifty times weaker than the player's. Both now use Impulse with the
/// same defaults, which means AI hits shove much harder than they used to.
/// Expect to retune - every constant is exposed on this component in the
/// Inspector.
/// </summary>
public class AIHitBox : BotHitBox
{
}
