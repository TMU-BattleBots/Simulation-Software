using UnityEngine;

public enum AIDifficulty
{
    Easy = 0,
    Medium = 1,
    Hard = 2,
}

/// <summary>
/// How one difficulty level changes the AI.
///
/// These are MULTIPLIERS on whatever is set on the AI component in the
/// Inspector, not absolute values. That way your tuning stays the source of
/// truth and difficulty scales it - retune the AI and all three levels move with
/// it. Medium is deliberately all 1.0, so Medium is exactly the AI as you tuned
/// it.
///
/// The interesting lever is reactionDelay. Rather than making the AI clumsy by
/// slowing it down, it delays what the AI can SEE - its vision cones report the
/// world as it was a fraction of a second ago. That is what a real perception
/// pipeline does (camera frame time plus inference time), so an easier bot is
/// not a stupider bot, it is a bot with worse sensors. It also makes the AI beatable
/// in a way that feels fair: you can outmanoeuvre it, rather than just out-running it.
/// </summary>
[System.Serializable]
public struct AIDifficultyProfile
{
    [Tooltip("Scales how fast the AI rotates to face its target. The single " +
             "biggest contributor to how dangerous it feels.")]
    public float trackingSpeedMultiplier;

    [Tooltip("Scales every movement speed on the AI.")]
    public float moveSpeedMultiplier;

    [Tooltip("Seconds of perception lag. The AI reacts to where the target WAS " +
             "this long ago. 0 = perfect, instant vision.")]
    public float reactionDelay;

    [Tooltip("Scales the AI's starting health.")]
    public float healthMultiplier;

    [Tooltip("Scales how long an investigation marker survives. Longer means the " +
             "AI commits harder to stale information, which makes it easier to bait.")]
    public float markerLifetimeMultiplier;

    [Tooltip("Scales how far the AI leads a moving target. 0 aims where you are " +
             "now and is easy to outrun; 1 is a full intercept.")]
    public float predictionMultiplier;

    public static AIDifficultyProfile For(AIDifficulty difficulty)
    {
        switch (difficulty)
        {
            case AIDifficulty.Easy:
                return new AIDifficultyProfile
                {
                    trackingSpeedMultiplier = 0.5f,   // turns lazily, easy to circle
                    moveSpeedMultiplier = 0.75f,
                    reactionDelay = 0.45f,            // clearly laggy vision
                    healthMultiplier = 0.8f,
                    markerLifetimeMultiplier = 1.6f,  // chases stale pings, easy to bait
                    predictionMultiplier = 0f,
                };

            case AIDifficulty.Hard:
                return new AIDifficultyProfile
                {
                    trackingSpeedMultiplier = 1.4f,   // snaps onto you
                    moveSpeedMultiplier = 1.15f,
                    reactionDelay = 0f,               // perfect vision
                    healthMultiplier = 1.2f,
                    markerLifetimeMultiplier = 0.6f,  // drops stale info quickly
                    predictionMultiplier = 1f,
                };

            case AIDifficulty.Medium:
            default:
                return new AIDifficultyProfile
                {
                    trackingSpeedMultiplier = 1f,
                    moveSpeedMultiplier = 1f,
                    reactionDelay = 0.15f,            // a touch of human-ish lag
                    healthMultiplier = 1f,
                    markerLifetimeMultiplier = 1f,
                    predictionMultiplier = 0.5f,
                };
        }
    }
}
