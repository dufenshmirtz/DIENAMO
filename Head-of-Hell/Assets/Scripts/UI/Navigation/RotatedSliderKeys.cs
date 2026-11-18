using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// For a horizontal Slider that is rotated 90 degrees so it LOOKS vertical (pause menu volume sliders):
/// Up/Down change the value, matching how it looks. Left/Right move to the other slider
/// (set as explicit Left/Right links on the Slider). Unity's own Slider would do it the other way round.
/// </summary>
[RequireComponent(typeof(Slider))]
public class RotatedSliderKeys : MonoBehaviour, IMoveHandler
{
    [Tooltip("How much one Up/Down press changes the value, as a fraction of the slider's range.")]
    [Range(0.01f, 1f)] public float stepFraction = 0.1f;
    [Tooltip("True if pressing Up should increase the value (slider rotated +90 degrees).")]
    public bool upIncreases = true;

    Slider slider;

    void Awake() => slider = GetComponent<Slider>();

    public void OnMove(AxisEventData eventData)
    {
        if (slider == null || !slider.IsInteractable()) return;

        float step = (slider.maxValue - slider.minValue) * stepFraction;
        if (slider.wholeNumbers) step = Mathf.Max(1f, Mathf.Round(step));

        if (eventData.moveDir == MoveDirection.Up)
            slider.value += upIncreases ? step : -step;
        else if (eventData.moveDir == MoveDirection.Down)
            slider.value -= upIncreases ? step : -step;
    }
}
