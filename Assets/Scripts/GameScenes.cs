/// <summary>
/// Scene names used for navigation.
///
/// The menu used to navigate with SceneManager.LoadScene(buildIndex + 1) and
/// (buildIndex + 2), which made the ORDER of the list in File > Build Settings
/// part of the game logic - reordering it silently sent the buttons to the
/// wrong scene with no error. Loading by name removes that coupling.
///
/// The scenes still have to be present in Build Settings (any order), otherwise
/// LoadScene throws at runtime.
/// </summary>
public static class GameScenes
{
    public const string MainMenu = "MainMenu";
    public const string PVP = "PVPv1";
    public const string PVE = "PVEv2working";
}
