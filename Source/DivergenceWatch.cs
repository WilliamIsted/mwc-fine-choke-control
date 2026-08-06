// Debug builds only - a development instrument, not something a player needs in a shipped
// mod. Every call site must carry the same guard, or Release fails to compile on a type
// that is not there.
#if DEBUG

using MSCLoader;
using UnityEngine;

namespace FineChokeControl
{
    /// <summary>
    /// Watches whether the previous frame's write survived, and says so when it did not.
    ///
    /// Vanilla writes the knob mesh from LateUpdate while the player holds the mouse, and
    /// MSCLoader offers no LateUpdate to write after it. That only matters on frames where
    /// the player scrolls and holds at once, which is rare enough to be hard to catch by
    /// eye - so the check records what was written and reads it back a frame later.
    /// </summary>
    internal static class DivergenceWatch
    {

        // Loose enough for float round-tripping through PlayMaker, tight enough that a
        // vanilla ramp step (1 per second on the chokes) always shows.
        private const float tolerance = 0.0005f;

        private static Control pending;
        private static float expectedValue;
        private static float expectedConsumer;
        private static float expectedMeshY;

        /// <summary>Records what a write left behind, for checking on the next frame.</summary>
        internal static void Record(Control control, float value)
        {
            pending = control;
            expectedValue = value;
            expectedConsumer = control.Consumer.Value;
            expectedMeshY = control.Mesh.localPosition.y;
        }

        /// <summary>
        /// Checks the recorded write and clears it. Call once per frame before anything
        /// else writes, so what it reads is whatever the game did in between.
        /// </summary>
        internal static void Poll()
        {

            if (pending == null)
            {
                return;
            }

            Control control = pending;
            pending = null;

            // The control going away between frames is a rebind, not a divergence.
            if (control.Mesh == null || control.Value == null || control.Consumer == null)
            {
                return;
            }

            Report(control, control.ValueVar, expectedValue, control.Value.Value);
            Report(control, control.ConsumerFsm + "." + control.ConsumerVar, expectedConsumer, control.Consumer.Value);
            Report(control, "mesh local Y", expectedMeshY, control.Mesh.localPosition.y);

        }

        private static void Report(Control control, string what, float expected, float actual)
        {

            if (Mathf.Abs(actual - expected) <= tolerance)
            {
                return;
            }

            ModConsole.Warning(
                $"[FineChokeControl] {control.Interaction}: {what} was written as {expected} and read back as " +
                $"{actual} a frame later. Something else is writing it - vanilla's LateUpdate is the likely one.");

        }

    }
}

#endif
