using System;
using System.Collections.Generic;

using HutongGames.PlayMaker;
using UnityEngine;

namespace FineChokeControl
{
    /// <summary>
    /// Provides definitions for the three fine-adjustable controls, keyed by the value
    /// <c>PlayerCurrentVehicle</c> holds while the player is sitting in that vehicle.
    /// </summary>
    public static class Definitions
    {

        /// <summary>
        /// Maps a vehicle name to the control it exposes. The key matches
        /// <c>PlayerCurrentVehicle</c>; <see cref="Control.Interaction"/> matches
        /// <c>GUIinteraction</c>, which both chokes set to the same "CHOKE" string.
        /// </summary>
        // No GameObject.Find here: static initializers run on first type touch, which can
        // be before the world loads, and the Corris choke rides on a removable dash part
        // that may not be assembled yet. Handles start null and Bind resolves them on
        // first use.
        public static Dictionary<string, Control> Controls = new Dictionary<string, Control>(StringComparer.OrdinalIgnoreCase)
        {
            {
                "Corris", new Control(
                    displayName: "Choke",
                    interaction: "CHOKE",
                    knobPath: "CORRIS/Assemblies/VINP_DashCoverBottom/Choke/ButtonChoke",
                    knobFsm: "Use",
                    valueVar: "Choke",
                    min: 1f,
                    max: 2f,
                    consumerPath: "CORRIS/Simulation/Engine/Fuel",
                    consumerFsm: "Mixture",
                    consumerVar: "Choke",
                    meshPath: "CORRIS/Assemblies/VINP_DashCoverBottom/Choke/ChokePivot/interior_dash_choke",
                    meshDivisor: 20f)
            },
            {
                "Sorbet", new Control(
                    displayName: "Choke",
                    interaction: "CHOKE",
                    knobPath: "SORBET(190-200psi)/Functions/Dashboard/Choke/ButtonChoke",
                    knobFsm: "Use",
                    valueVar: "Choke",
                    min: 1f,
                    max: 2f,
                    consumerPath: "SORBET(190-200psi)/Simulation/Engine/EngineSim",
                    consumerFsm: "OperatingTemp",
                    consumerVar: "Choke",
                    meshPath: "SORBET(190-200psi)/Functions/Dashboard/Choke/ChokePivot/Lever",
                    meshDivisor: 20f,
                    // Only the Sorbet has a dash warning light, and vanilla toggles it from
                    // a state transition that a mod-driven change never reaches, so the mod
                    // has to drive it directly.
                    lightPath: "SORBET(190-200psi)/Functions/Dashboard/DashSymbols/DashLightChoke",
                    lightThreshold: 1.1f)
            },
            {
                "Gifu", new Control(
                    displayName: "Hand throttle",
                    interaction: "HAND THROTTLE",
                    knobPath: "GIFU(750/450psi)/LOD/Dashboard/ButtonHandThrottle",
                    knobFsm: "Use",
                    valueVar: "LeverPos",
                    min: 0.008f,
                    max: 0.03f,
                    // The hand throttle's consumer FSM sits on the button itself, not under
                    // Simulation/Engine like the chokes.
                    consumerPath: "GIFU(750/450psi)/LOD/Dashboard/ButtonHandThrottle",
                    consumerFsm: "Throttle",
                    consumerVar: "Throttle",
                    meshPath: "GIFU(750/450psi)/LOD/Dashboard/KnobHandThrottle/Knob",
                    // The lever's own value is the mesh Y, so no division.
                    meshDivisor: 1f,
                    // LeverPos feeds a second variable on the same FSM before it reaches the
                    // consumer. The chokes have no equivalent, so these stay unset there.
                    derivedVar: "Throttle",
                    derivedFactor: 14.8f,
                    derivedMin: 0.13f,
                    derivedMax: 1f)
            }
        };

    }

    /// <summary>
    /// One fine-adjustable control: the vanilla constants the mod is built against, plus
    /// the runtime handles resolved on first use.
    /// </summary>
    public sealed class Control
    {

        /// <summary>Name shown in the on-screen readout.</summary>
        public string DisplayName { get; }

        /// <summary>Value <c>GUIinteraction</c> holds while the player is aimed at this control.</summary>
        public string Interaction { get; }

        /// <summary>Path to the object that receives the click, which is not the object that moves.</summary>
        public string KnobPath { get; }

        /// <summary>FSM on the knob object holding the control's own value.</summary>
        public string KnobFsm { get; }

        /// <summary>The control's own value: Choke on both chokes, LeverPos on the Gifu.</summary>
        public string ValueVar { get; }

        /// <summary>Bottom of the vanilla clamp on <see cref="ValueVar"/>.</summary>
        public float Min { get; }

        /// <summary>Top of the vanilla clamp on <see cref="ValueVar"/>.</summary>
        public float Max { get; }

        /// <summary>Path to the object holding the FSM that actually drives the engine.</summary>
        public string ConsumerPath { get; }

        /// <summary>FSM the engine reads from.</summary>
        public string ConsumerFsm { get; }

        /// <summary>Variable the engine reads. Vanilla only copies to it while its own state runs.</summary>
        public string ConsumerVar { get; }

        /// <summary>Path to the mesh that visibly moves, a sibling subtree of the knob object.</summary>
        public string MeshPath { get; }

        /// <summary>Divisor turning <see cref="ValueVar"/> into the mesh's local Y. 1 means the value is the Y.</summary>
        public float MeshDivisor { get; }

        /// <summary>Second variable derived from <see cref="ValueVar"/>, or null where there is none.</summary>
        public string DerivedVar { get; }

        /// <summary>Multiplier producing <see cref="DerivedVar"/>.</summary>
        public float DerivedFactor { get; }

        /// <summary>Bottom of the vanilla clamp on <see cref="DerivedVar"/>.</summary>
        public float DerivedMin { get; }

        /// <summary>Top of the vanilla clamp on <see cref="DerivedVar"/>.</summary>
        public float DerivedMax { get; }

        /// <summary>Path to the dash warning light, or null where there is none.</summary>
        public string LightPath { get; }

        /// <summary>Value of <see cref="ValueVar"/> above which the light comes on.</summary>
        public float LightThreshold { get; }

        // Handles below are resolved by FineChokeControl.Bind and cleared when the scene
        // takes them away.

        /// <summary>The control's own value on the knob FSM.</summary>
        public FsmFloat Value;

        /// <summary>The variable the engine reads.</summary>
        public FsmFloat Consumer;

        /// <summary>The derived variable, or null on controls without one.</summary>
        public FsmFloat Derived;

        /// <summary>The mesh transform. Doubles as the bound flag, so it is assigned last.</summary>
        public Transform Mesh;

        /// <summary>The dash light, or null on controls without one.</summary>
        public GameObject Light;

#if DEBUG
        /// <summary>Whether a bind failure has already been reported for this control.</summary>
        // Bind runs on every scroll notch until it succeeds, so without this the console
        // fills with the same line while the player sits there scrolling at nothing.
        public bool BindWarned;
#endif

        /// <summary>
        /// Creates a control definition. Optional arguments cover the parts only one
        /// vehicle has: the Gifu's derived throttle and the Sorbet's warning light.
        /// </summary>
        public Control(string displayName, string interaction, string knobPath, string knobFsm, string valueVar,
            float min, float max, string consumerPath, string consumerFsm, string consumerVar, string meshPath,
            float meshDivisor, string derivedVar = null, float derivedFactor = 0f, float derivedMin = 0f,
            float derivedMax = 0f, string lightPath = null, float lightThreshold = 0f)
        {
            DisplayName = displayName;
            Interaction = interaction;
            KnobPath = knobPath;
            KnobFsm = knobFsm;
            ValueVar = valueVar;
            Min = min;
            Max = max;
            ConsumerPath = consumerPath;
            ConsumerFsm = consumerFsm;
            ConsumerVar = consumerVar;
            MeshPath = meshPath;
            MeshDivisor = meshDivisor;
            DerivedVar = derivedVar;
            DerivedFactor = derivedFactor;
            DerivedMin = derivedMin;
            DerivedMax = derivedMax;
            LightPath = lightPath;
            LightThreshold = lightThreshold;
        }

        /// <summary>
        /// Fraction of the control's travel currently dialled in, 0 to 1. The chokes read
        /// (Choke - 1), the Gifu reads (LeverPos - 0.008) / 0.022.
        /// </summary>
        public float Fraction(float value)
        {
            return (value - Min) / (Max - Min);
        }

    }
}
