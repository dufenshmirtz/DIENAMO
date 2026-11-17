/// <summary>
/// Implemented by a menu script that wants Cancel (Esc / gamepad B) to undo something first
/// (e.g. the last character pick) before MenuNavigator presses the panel's Back button.
/// Return true if something was undone (then Back is NOT pressed).
/// </summary>
public interface IMenuCancelHandler
{
    bool TryCancel();
}
