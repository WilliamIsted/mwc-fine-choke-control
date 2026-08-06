using HutongGames.PlayMaker;
using MSCLoader;
using UnityEngine;

namespace FineChokeControl
{

    public class FineChokeControl : Mod
    {

        public override string ID => "FineChokeControl";
        public override string Name => "Fine Choke Control";
        public override string Author => "WilliamIsted";
        public override string Version => "0.0.1";
        public override string Description => "Scroll-wheel fine adjustment for the Corris and Sorbet choke and the Gifu hand throttle.";
        public override Game SupportedGames => Game.MyWinterCar;

        private FsmVariables globalVars = null;
        private FsmBool guiUse = null;
        private FsmString guiInteraction = null;
        private FsmString currentVehicle = null;

        public override void ModSetup()
        {
            SetupFunction(Setup.OnLoad, OnLoad);
            SetupFunction(Setup.Update, Update);
            SetupFunction(Setup.OnGUI, OnGUI);
            SetupFunction(Setup.ModSettings, ModSettings);
        }

        private void ModSettings()
        {
            // All settings should be created here.
            // DO NOT put anything that isn't settings or keybinds in here!

            SettingsManager.InitSettings();
        }

        private void OnLoad()
        {
            // Called once, when mod is loading after game is fully loaded

            globalVars = PlayMakerGlobals.Instance.Variables;

            guiUse = globalVars.FindFsmBool("GUIuse");
            guiInteraction = globalVars.FindFsmString("GUIinteraction");
            currentVehicle = globalVars.FindFsmString("PlayerCurrentVehicle");

            // The scene is rebuilt on every save load, so handles resolved last time point
            // at objects that no longer exist. Bind runs again on first use.
            foreach (Control control in Definitions.Controls.Values)
                Release(control);

#if DEBUG
            FsmSpecCheck.Run();
#endif
        }

        private void Update()
        {

#if DEBUG
            // Before the early returns: last frame's write has to be read back whether or
            // not the player is still scrolling.
            DivergenceWatch.Poll();
#endif

            // Cheapest first. Both are plain input reads and both are idle on almost every
            // frame, which keeps the FSM reads and the scene lookup off the hot path.
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            bool jump = Input.GetMouseButtonDown(2);

            if (scroll == 0f && !jump)
            {
                return;
            }

            Control control = GetActiveControl();
            if (control == null)
            {
                return;
            }

            if (jump)
            {
                // Whichever end is further away, so a half-open choke closes rather than
                // creeping to the nearer limit.
                bool belowHalf = control.Fraction(control.Value.Value) < 0.5f;
                Apply(control, belowHalf ? control.Max : control.Min);
                return;
            }

            if (SettingsManager.invertScroll)
            {
                scroll = -scroll;
            }

            // Sign, not magnitude. One notch is one step whatever the player's wheel
            // reports, and how big that step is belongs to the setting.
            Apply(control, control.Value.Value + Travel(control) * Mathf.Sign(scroll));

        }

        /// <summary>How far one scroll notch moves this control, in its own units.</summary>
        private float Travel(Control control)
        {
            return (control.Max - control.Min) * SettingsManager.stepFraction;
        }

        private void OnGUI()
        {

            if (!SettingsManager.showReadout)
            {
                return;
            }

            Readout.Draw();

        }

        /*
         *
         *
         *
         */

        /// <summary>
        /// Writes a value to a control: its own variable, the variable the engine reads,
        /// the mesh that moves, and the dash light where there is one.
        /// </summary>
        // Vanilla only copies the knob's value to the engine and to the mesh from inside
        // its On/Off/INCREASE/DECREASE states, which run while the player holds the mouse
        // and never otherwise. Writing the knob variable alone changes nothing audible, so
        // every write here has to do the whole job.
        private void Apply(Control control, float requested)
        {

            float value = Mathf.Clamp(requested, control.Min, control.Max);

            control.Value.Value = value;

            if (control.Derived != null)
            {
                // The Gifu's engine reads Throttle, not LeverPos. Both clamps are vanilla's.
                float derived = Mathf.Clamp(value * control.DerivedFactor, control.DerivedMin, control.DerivedMax);
                control.Derived.Value = derived;
                control.Consumer.Value = derived;
            }
            else
            {
                control.Consumer.Value = value;
            }

            Vector3 position = control.Mesh.localPosition;
            control.Mesh.localPosition = new Vector3(position.x, value / control.MeshDivisor, position.z);

            if (control.Light != null)
            {
                control.Light.SetActive(value > control.LightThreshold);
            }

            if (SettingsManager.showReadout)
            {
                Readout.Show(control.DisplayName, control.Fraction(value));
            }

#if DEBUG
            DivergenceWatch.Record(control, value);
#endif

        }

        /// <summary>
        /// Returns the control the player is currently aimed at, or null when they are not
        /// aimed at one or it cannot be resolved yet.
        /// </summary>
        private Control GetActiveControl()
        {

            if (guiUse == null || !guiUse.Value)
            {
                return null;
            }

            Control control;
            if (!Definitions.Controls.TryGetValue(currentVehicle.Value, out control))
            {
                return null;
            }

            // Both chokes report "CHOKE", so the vehicle alone is not enough to know the
            // player is on the choke rather than some other control in the same cab.
            if (guiInteraction.Value != control.Interaction)
            {
                return null;
            }

            if (control.Mesh != null)
            {
                return control;
            }

            return Bind(control) ? control : null;

        }

        /// <summary>
        /// Resolves a control's runtime handles. All or nothing: a partial resolve leaves
        /// every handle null so the next attempt starts clean.
        /// </summary>
        private bool Bind(Control control)
        {

            Release(control);

            GameObject knob = GameObject.Find(control.KnobPath);
            if (knob == null)
            {
                return BindFailed(control, "no object at " + control.KnobPath);
            }

            GameObject consumer = GameObject.Find(control.ConsumerPath);
            if (consumer == null)
            {
                return BindFailed(control, "no object at " + control.ConsumerPath);
            }

            GameObject mesh = GameObject.Find(control.MeshPath);
            if (mesh == null)
            {
                return BindFailed(control, "no object at " + control.MeshPath);
            }

            PlayMakerFSM knobFsm = knob.GetPlayMaker(control.KnobFsm);
            if (knobFsm == null)
            {
                return BindFailed(control, "no FSM \"" + control.KnobFsm + "\" on " + control.KnobPath);
            }

            PlayMakerFSM consumerFsm = consumer.GetPlayMaker(control.ConsumerFsm);
            if (consumerFsm == null)
            {
                return BindFailed(control, "no FSM \"" + control.ConsumerFsm + "\" on " + control.ConsumerPath);
            }

            FsmFloat value = knobFsm.FsmVariables.FindFsmFloat(control.ValueVar);
            if (value == null)
            {
                return BindFailed(control, "FSM \"" + control.KnobFsm + "\" has no float \"" + control.ValueVar + "\"");
            }

            FsmFloat consumerValue = consumerFsm.FsmVariables.FindFsmFloat(control.ConsumerVar);
            if (consumerValue == null)
            {
                return BindFailed(control, "FSM \"" + control.ConsumerFsm + "\" has no float \"" + control.ConsumerVar + "\"");
            }

            FsmFloat derived = null;
            if (control.DerivedVar != null)
            {
                derived = knobFsm.FsmVariables.FindFsmFloat(control.DerivedVar);
                if (derived == null)
                {
                    return BindFailed(control, "FSM \"" + control.KnobFsm + "\" has no float \"" + control.DerivedVar + "\"");
                }
            }

            GameObject light = null;
            if (control.LightPath != null)
            {
                light = GameObject.Find(control.LightPath);
                if (light == null)
                {
                    return BindFailed(control, "no dash light at " + control.LightPath);
                }
            }

            control.Value = value;
            control.Consumer = consumerValue;
            control.Derived = derived;
            control.Light = light;

            // Assigned last: Mesh is the only UnityEngine.Object in the set, so its
            // overloaded null check is what detects the control being destroyed later, and
            // a partial resolve must never look complete.
            control.Mesh = mesh.transform;

#if DEBUG
            control.BindWarned = false;
#endif

            return true;

        }

        /// <summary>
        /// Fails a bind, saying why once per control in Debug builds. Anything the player
        /// can cause - a dash cover in the boot, a vehicle not spawned yet - reads the same
        /// as a path gone stale after a game update, and only the console tells them apart.
        /// </summary>
        private bool BindFailed(Control control, string reason)
        {

#if DEBUG
            // Bind retries on every scroll notch until it succeeds, so this reports the
            // first failure and then goes quiet until something binds.
            if (!control.BindWarned)
            {
                control.BindWarned = true;
                ModConsole.Warning($"[FineChokeControl] {control.Interaction}: {reason}");
            }
#endif

            return false;

        }

        /// <summary>Drops a control's handles so the next use rebinds.</summary>
        private void Release(Control control)
        {
            control.Value = null;
            control.Consumer = null;
            control.Derived = null;
            control.Mesh = null;
            control.Light = null;
        }

    }

}
