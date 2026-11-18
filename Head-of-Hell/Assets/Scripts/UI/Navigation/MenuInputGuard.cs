using UnityEngine;

/// <summary>
/// Tells gameplay code that a menu is (or just was) open, so keys that menus use (e.g. Enter)
/// don't also trigger gameplay shortcuts like GameManager's Enter = quick restart.
/// "Just was" covers the frame in which a menu button closes the menu: scripts run in an
/// undefined order, so GameManager may run after the menu closed but in the same frame.
/// </summary>
public static class MenuInputGuard
{
    static int lastOpenFrame = -10;

    /// <summary>Call every frame while a menu is open.</summary>
    public static void MarkOpen() => lastOpenFrame = Time.frameCount;

    /// <summary>True if a menu was open this frame or the previous one.</summary>
    public static bool MenuOpen => Time.frameCount - lastOpenFrame <= 1;
}
