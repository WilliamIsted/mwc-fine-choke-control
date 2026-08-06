using MSCLoader;

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

        /// <summary>Share of the control's full travel applied per scroll notch, 0 to 1.</summary>
        // Expressed as a percentage in the UI because the underlying ranges differ per
        // control (1 to 2 on the chokes, 0.008 to 0.03 on the Gifu). A percentage means one
        // setting that feels the same on all three.
        public static float stepFraction => settingStepPercent.GetValue() / 100f;

        /// <summary>Whether scrolling up closes the control rather than opening it.</summary>
        public static bool invertScroll => settingInvertScroll.GetValue();

        /// <summary>Whether the on-screen readout is drawn while adjusting.</summary>
        public static bool showReadout => settingShowReadout.GetValue();

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

        }

    }
}
