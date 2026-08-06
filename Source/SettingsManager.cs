using MSCLoader;
using UnityEngine;

namespace FineChokeControl
{
    /// <summary>
    /// MSCLoader settings tab. Holds the tuning the player is expected to want and nothing
    /// else - the control definitions themselves are vanilla constants, not preferences.
    /// </summary>
    public static class SettingsManager
    {

        private static SettingsSlider settingStepPercent;
        private static SettingsCheckBox settingInvertScroll;
        private static SettingsCheckBox settingShowReadout;

        private static SettingsKeybind kbIncrease;
        private static SettingsKeybind kbDecrease;
        private static SettingsKeybind kbToggle;

        /// <summary>Share of the control's full travel applied per scroll notch, 0 to 1.</summary>
        // Expressed as a percentage in the UI because the underlying ranges differ per
        // control (1 to 2 on the chokes, 0.008 to 0.03 on the Gifu). A percentage means one
        // setting that feels the same on all three.
        public static float stepFraction => settingStepPercent.GetValue() / 100f;

        /// <summary>Whether scrolling up closes the control rather than opening it.</summary>
        public static bool invertScroll => settingInvertScroll.GetValue();

        /// <summary>Whether the on-screen readout is drawn while adjusting.</summary>
        public static bool showReadout => settingShowReadout.GetValue();

        /// <summary>Whether the open/increase key is held this frame.</summary>
        public static bool increaseHeld => kbIncrease.GetKeybind();

        /// <summary>Whether the close/decrease key is held this frame.</summary>
        public static bool decreaseHeld => kbDecrease.GetKeybind();

        /// <summary>Whether the toggle key was pressed this frame.</summary>
        public static bool togglePressed => kbToggle.GetKeybindDown();

        /// <summary>
        /// Creates the settings. Called from ModSettings, which is the only place MSCLoader
        /// allows settings to be built.
        /// </summary>
        public static void InitSettings()
        {

            Settings.AddText("Sensitivity of mouse wheel scroll while looking at the choke or hand throttle.");

            settingStepPercent = Settings.AddSlider("stepPercent", "", 1f, 25f, 5f, null, 0);
            settingInvertScroll = Settings.AddCheckBox("invertScroll", "Invert Scroll Wheel", false);
            settingShowReadout = Settings.AddCheckBox("showReadout", "Show adjustment percentage while adjusting", true);

            Settings.AddHeader("Keybinds");
            Settings.AddText("These work anywhere in the vehicle, without looking at the control, so it can be adjusted while driving. The choke in the Corris and Sorbet, hand throttle in the Gifu.");

            // Labels avoid naming the choke because the same keys drive the Gifu's hand
            // throttle. Defaults match the MSC mod.
            kbIncrease = Keybind.Add("increase", "Open Choke / Increase Throttle (hold)", KeyCode.PageUp);
            kbDecrease = Keybind.Add("decrease", "Close Choke / Decrease Throttle (hold)", KeyCode.PageDown);
            kbToggle = Keybind.Add("toggle", "Toggle Between Fully Open and Fully Closed", KeyCode.Home);

        }

    }
}
