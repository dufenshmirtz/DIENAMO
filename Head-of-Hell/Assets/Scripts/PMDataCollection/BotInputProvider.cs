using UnityEngine;
using System.Collections.Generic;

public class BotInputProvider : IInputProvider
{
    private readonly Dictionary<string, float> axes = new Dictionary<string, float>();
    private readonly Dictionary<KeyCode, bool> heldKeys = new Dictionary<KeyCode, bool>();

    private readonly HashSet<KeyCode> downKeys = new HashSet<KeyCode>();
    private readonly HashSet<KeyCode> upKeys = new HashSet<KeyCode>();

    private readonly HashSet<string> downButtons = new HashSet<string>();
    private readonly HashSet<string> upButtons = new HashSet<string>();

    public float GetAxis(string name)
    {
        return axes.TryGetValue(name, out float value) ? value : 0f;
    }

    public bool GetButtonDown(string name) => downButtons.Contains(name);
    public bool GetButtonUp(string name) => upButtons.Contains(name);
    public bool GetKeyDown(KeyCode key) => downKeys.Contains(key);
    public bool GetKeyUp(KeyCode key) => upKeys.Contains(key);
    public bool GetKey(KeyCode key) => heldKeys.ContainsKey(key) && heldKeys[key];

    public void SetAxis(string name, float value)
    {
        axes[name] = Mathf.Clamp(value, -1f, 1f);
    }

    public void SetKey(KeyCode key, bool pressed)
    {
        bool wasHeld = heldKeys.ContainsKey(key) && heldKeys[key];

        if (pressed && !wasHeld)
            downKeys.Add(key);

        if (!pressed && wasHeld)
            upKeys.Add(key);

        heldKeys[key] = pressed;
    }

    // DAgger shadow expert (23/10): change the held state WITHOUT a key-down / key-up event (used to re-sync the shadow
    // bot's intention with what the agent really did; a key-up here would be logged as a charge release).
    public void ForceKeyState(KeyCode key, bool held)
    {
        heldKeys[key] = held;
    }

    public void PressKeyOneFrame(KeyCode key)
    {
        downKeys.Add(key);
        upKeys.Add(key);
        heldKeys[key] = false;
    }

    public void ClearFrameState()
    {
        downKeys.Clear();
        upKeys.Clear();
        downButtons.Clear();
        upButtons.Clear();
    }
}