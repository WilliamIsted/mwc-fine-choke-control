// Debug builds only — this is a development safety net, not something a player needs
// in a shipped mod. The call site in FineChokeControl.OnLoad must carry the same guard,
// or Release fails to compile on a type that is not there.
#if DEBUG

using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using MSCLoader;
using UnityEngine;

namespace FineChokeControl
{
    /// <summary>
    /// Compares the vanilla FSM constants this mod hard-codes against what the game
    /// actually ships, and warns when they no longer agree.
    ///
    /// Purely diagnostic — it reads, reports and changes nothing. The point is to find
    /// out on the first launch after a game update, from the console, rather than from
    /// a knob that quietly sits in the wrong place.
    ///
    /// Every expected value below was read out of the running game with the knob probe;
    /// the raw action dumps are the source of truth for what each number means.
    /// </summary>
    internal static class FsmSpecCheck
    {
        // OnLoad fires per save load. This makes the check once per process.
        private static bool hasRun;

        private const float Tolerance = 0.0001f;

        private struct ClampSpec
        {
            public string Variable;
            public float Min;
            public float Max;

            public ClampSpec(string variable, float min, float max)
            {
                Variable = variable;
                Min = min;
                Max = max;
            }
        }

        private struct OperatorSpec
        {
            public string Result;
            public float Operand;
            public FloatOperator.Operation Operation;

            public OperatorSpec(string result, FloatOperator.Operation operation, float operand)
            {
                Result = result;
                Operation = operation;
                Operand = operand;
            }
        }

        private sealed class ControlSpec
        {
            public string Label;
            public string Path;
            public string Fsm;
            public string State;
            public ClampSpec[] Clamps;
            public OperatorSpec[] Operators;
        }

        // Both chokes are identical in every number; only the object path differs.
        private static readonly ControlSpec[] Specs =
        {
            new ControlSpec
            {
                Label = "Corris choke",
                Path = "CORRIS/Assemblies/VINP_DashCoverBottom/Choke/ButtonChoke",
                Fsm = "Use",
                State = "On",
                Clamps = new[] { new ClampSpec("Choke", 1f, 2f) },
                // Pos = Choke / 20, and Pos is the knob mesh's local Y.
                Operators = new[] { new OperatorSpec("Pos", FloatOperator.Operation.Divide, 20f) },
            },
            new ControlSpec
            {
                Label = "Sorbet choke",
                Path = "SORBET(190-200psi)/Functions/Dashboard/Choke/ButtonChoke",
                Fsm = "Use",
                State = "On",
                Clamps = new[] { new ClampSpec("Choke", 1f, 2f) },
                Operators = new[] { new OperatorSpec("Pos", FloatOperator.Operation.Divide, 20f) },
            },
            new ControlSpec
            {
                Label = "Gifu hand throttle",
                Path = "GIFU(750/450psi)/LOD/Dashboard/ButtonHandThrottle",
                Fsm = "Use",
                State = "INCREASE",
                // Two clamps in this state: the lever's own travel, then the throttle
                // derived from it. The lever's minimum yields 0.1184, under the 0.13
                // floor, so the bottom of the travel is a dead zone.
                Clamps = new[]
                {
                    new ClampSpec("LeverPos", 0.008f, 0.03f),
                    new ClampSpec("Throttle", 0.13f, 1f),
                },
                Operators = new[] { new OperatorSpec("Throttle", FloatOperator.Operation.Multiply, 14.8f) },
            },
        };

        /// <summary>Run once per process. Safe to call from OnLoad on every save load.</summary>
        internal static void Run()
        {
            if (hasRun) return;
            hasRun = true;

            int problems = 0;

            foreach (ControlSpec spec in Specs)
            {
                try
                {
                    problems += Check(spec);
                }
                catch (System.Exception e)
                {
                    // A failed check must never take a working control down with it.
                    ModConsole.Warning($"[FineChokeControl] Could not verify {spec.Label}: {e.Message}");
                    problems++;
                }
            }

            if (problems == 0)
            {
                ModConsole.Print("[FineChokeControl] FSM constants match the values this mod was built against.");
            }
            else
            {
                ModConsole.Warning(
                    $"[FineChokeControl] {problems} FSM constant(s) no longer match. The mod still uses its own " +
                    "values, so the knob may sit wrong or the range may be off. Update the constants to the " +
                    "actual values logged above.");
            }
        }

        private static int Check(ControlSpec spec)
        {
            GameObject go = GameObject.Find(spec.Path);
            if (go == null)
            {
                ModConsole.Warning($"[FineChokeControl] {spec.Label}: object not found at {spec.Path}");
                return 1;
            }

            PlayMakerFSM fsm = go.GetPlayMaker(spec.Fsm);
            if (fsm == null)
            {
                ModConsole.Warning($"[FineChokeControl] {spec.Label}: no FSM named \"{spec.Fsm}\" on {spec.Path}");
                return 1;
            }

            FsmState state = FindState(fsm, spec.State);
            if (state == null)
            {
                ModConsole.Warning($"[FineChokeControl] {spec.Label}: FSM \"{spec.Fsm}\" has no state \"{spec.State}\"");
                return 1;
            }

            // An FSM that has never run has no action instances yet — the Corris choke
            // sits on a removable dash part, so this is reachable in normal play.
            if (state.Actions == null || state.Actions.Length == 0)
            {
                fsm.InitializeFSM();
                state = FindState(fsm, spec.State);
            }

            if (state == null || state.Actions == null)
            {
                ModConsole.Warning($"[FineChokeControl] {spec.Label}: state \"{spec.State}\" has no actions to read");
                return 1;
            }

            int problems = 0;

            foreach (ClampSpec expected in spec.Clamps)
            {
                problems += CheckClamp(spec, state, expected);
            }

            foreach (OperatorSpec expected in spec.Operators)
            {
                problems += CheckOperator(spec, state, expected);
            }

            return problems;
        }

        private static int CheckClamp(ControlSpec spec, FsmState state, ClampSpec expected)
        {
            // Matched by the variable it clamps, not by position — the Gifu state holds
            // two clamps and a reordering there would otherwise read as a value change.
            FloatClamp action = null;
            foreach (FsmStateAction candidate in state.Actions)
            {
                FloatClamp clamp = candidate as FloatClamp;
                if (clamp != null && clamp.floatVariable != null && clamp.floatVariable.Name == expected.Variable)
                {
                    action = clamp;
                    break;
                }
            }

            if (action == null)
            {
                ModConsole.Warning(
                    $"[FineChokeControl] {spec.Label}: no FloatClamp on \"{expected.Variable}\" in state \"{spec.State}\"");
                return 1;
            }

            int problems = 0;
            problems += Compare(spec, $"{expected.Variable} clamp min", expected.Min, action.minValue);
            problems += Compare(spec, $"{expected.Variable} clamp max", expected.Max, action.maxValue);
            return problems;
        }

        private static int CheckOperator(ControlSpec spec, FsmState state, OperatorSpec expected)
        {
            FloatOperator action = null;
            foreach (FsmStateAction candidate in state.Actions)
            {
                FloatOperator op = candidate as FloatOperator;
                if (op != null && op.storeResult != null && op.storeResult.Name == expected.Result)
                {
                    action = op;
                    break;
                }
            }

            if (action == null)
            {
                ModConsole.Warning(
                    $"[FineChokeControl] {spec.Label}: no FloatOperator storing \"{expected.Result}\" in state \"{spec.State}\"");
                return 1;
            }

            int problems = 0;

            if (action.operation != expected.Operation)
            {
                ModConsole.Warning(
                    $"[FineChokeControl] {spec.Label}: {expected.Result} operation is {action.operation}, expected {expected.Operation}");
                problems++;
            }

            problems += Compare(spec, $"{expected.Result} operand", expected.Operand, action.float2);
            return problems;
        }

        private static int Compare(ControlSpec spec, string what, float expected, FsmFloat actual)
        {
            if (actual == null)
            {
                ModConsole.Warning($"[FineChokeControl] {spec.Label}: {what} is missing, expected {expected}");
                return 1;
            }

            // A literal turned into a variable reference is drift too, even if today's
            // value happens to agree — the name is reported so it is obvious which.
            if (Mathf.Abs(actual.Value - expected) > Tolerance)
            {
                string source = string.IsNullOrEmpty(actual.Name) ? "" : $" (from variable \"{actual.Name}\")";
                ModConsole.Warning(
                    $"[FineChokeControl] {spec.Label}: {what} is {actual.Value}{source}, expected {expected}");
                return 1;
            }

            return 0;
        }

        private static FsmState FindState(PlayMakerFSM fsm, string name)
        {
            if (fsm.FsmStates == null) return null;

            foreach (FsmState state in fsm.FsmStates)
            {
                if (state != null && state.Name == name) return state;
            }

            return null;
        }
    }
}

#endif
