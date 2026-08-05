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
        public override string Description => "This mod adds fine control over the Corris and Sorbet choke via the scroll wheel as well as the Gifu hand throttle.";
        public override Game SupportedGames => Game.MyWinterCar;

        public override void ModSetup()
        {
            SetupFunction(Setup.OnLoad, OnLoad);
            SetupFunction(Setup.Update, Update);
            SetupFunction(Setup.ModSettings, ModSettings);
        }

        private void ModSettings()
        {
            // All settings should be created here. 
            // DO NOT put anything that isn't settings or keybinds in here!
        }

        private void OnLoad()
        {
            // Called once, when mod is loading after game is fully loaded

#if DEBUG
            FsmSpecCheck.Run();
#endif
        }

        private void Update()
        {
            // Update is called once per frame
        }

    }

}