using Harmony;
using System.Reflection;

namespace RandomTravelContracts {
    public class RandomTravelContracts {
        public static void Init() {
            var harmony = HarmonyInstance.Create("de.morphyum.RandomTravelContracts");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }

    


}
