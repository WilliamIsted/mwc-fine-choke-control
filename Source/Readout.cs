using UnityEngine;

namespace FineChokeControl
{
    /// <summary>
    /// Draws the control's current value under the game's interaction label while the
    /// player is adjusting it, then fades out.
    /// </summary>
    public static class Readout
    {

        private const float holdSeconds = 1.2f;
        private const float fadeSeconds = 0.4f;

        // Clear of the interaction label, which MWC draws just under the crosshair rather
        // than at the foot of the screen as MSC did. Tune here if it sits wrong.
        private const float belowCrosshair = 96f;

        private static string text = "";
        private static float hideAt;
        private static GUIStyle style;

        /// <summary>
        /// Shows a control's value, restarting the hold. Called on every adjustment, so it
        /// stays visible for as long as the player keeps scrolling.
        /// </summary>
        public static void Show(string displayName, float fraction)
        {
            text = displayName + " " + Mathf.RoundToInt(fraction * 100f) + "%";
            hideAt = Time.realtimeSinceStartup + holdSeconds;
        }

        /// <summary>Clears the readout immediately, for when the player looks away mid-fade.</summary>
        public static void Hide()
        {
            hideAt = 0f;
        }

        /// <summary>Draws the readout. Call from OnGUI; returns straight away when there is nothing to draw.</summary>
        public static void Draw()
        {

            // realtimeSinceStartup rather than Time.time: the readout should still fade
            // while the game is paused in a menu, and Time.time stops there.
            float remaining = hideAt - Time.realtimeSinceStartup;
            if (remaining <= -fadeSeconds)
            {
                return;
            }

            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label);
                style.alignment = TextAnchor.UpperCenter;
                style.fontSize = 18;
                style.fontStyle = FontStyle.Bold;
            }

            float alpha = remaining >= 0f ? 1f : 1f + (remaining / fadeSeconds);

            Rect rect = new Rect(0f, (Screen.height / 2f) + belowCrosshair, Screen.width, 30f);

            // Drawn twice, offset by a pixel, because pale dashboards swallow white text.
            Color previous = GUI.color;

            GUI.color = new Color(0f, 0f, 0f, alpha * 0.75f);
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, style);

            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(rect, text, style);

            GUI.color = previous;

        }

    }
}
