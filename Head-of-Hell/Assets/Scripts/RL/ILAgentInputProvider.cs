using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Input provider driven by the imitation-learning agent (ILAgentController).
/// Same frame semantics as BotInputProvider (held keys + one-frame key-down/key-up), but a separate class so that
/// recorders and telemetry can tell the IL agent apart from the scripted bot.
/// </summary>
public class ILAgentInputProvider : IInputProvider
{
    private readonly Dictionary<string, float> axes = new Dictionary<string, float>();
    private readonly Dictionary<KeyCode, bool> heldKeys = new Dictionary<KeyCode, bool>();
    private readonly HashSet<KeyCode> downKeys = new HashSet<KeyCode>();
    private readonly HashSet<KeyCode> upKeys = new HashSet<KeyCode>();

    public float GetAxis(string name) => axes.TryGetValue(name, out float v) ? v : 0f;
    public bool GetButtonDown(string name) => false;
    public bool GetButtonUp(string name) => false;
    public bool GetKeyDown(KeyCode key) => downKeys.Contains(key);
    public bool GetKeyUp(KeyCode key) => upKeys.Contains(key);
    public bool GetKey(KeyCode key) => heldKeys.TryGetValue(key, out bool held) && held;

    public void SetAxis(string name, float value) => axes[name] = Mathf.Clamp(value, -1f, 1f);

    /// Held key; produces one key-down / key-up frame on a change.
    public void SetKey(KeyCode key, bool pressed)
    {
        bool wasHeld = GetKey(key);
        if (pressed && !wasHeld) downKeys.Add(key);
        if (!pressed && wasHeld) upKeys.Add(key);
        heldKeys[key] = pressed;
    }

    /// Tap: key-down and key-up in the same frame (like BotInputProvider.PressKeyOneFrame).
    public void PressKeyOneFrame(KeyCode key)
    {
        downKeys.Add(key);
        upKeys.Add(key);
        heldKeys[key] = false;
    }

    /// Called at the start of every frame by the controller: one-frame events last exactly one frame.
    public void ClearFrameState()
    {
        downKeys.Clear();
        upKeys.Clear();
    }

    public void ReleaseAll()
    {
        foreach (var k in new List<KeyCode>(heldKeys.Keys)) SetKey(k, false);
        foreach (var a in new List<string>(axes.Keys)) axes[a] = 0f;
    }
}
