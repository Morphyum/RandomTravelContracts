using BattleTech;
using BattleTech.Framework;

namespace RandomTravelContracts {

    public class Settings {
        public float percentageOfTravelOnBorder = 0.5f;
    }

    public static class Fields {
        public static Settings settings;
        public static int currBorderCons = 0;
    }

    public struct PotentialContract {
        // Token: 0x040089A4 RID: 35236
        public ContractOverride contractOverride;

        // Token: 0x040089A5 RID: 35237
        public Faction employer;

        // Token: 0x040089A6 RID: 35238
        public Faction target;

        // Token: 0x040089A7 RID: 35239
        public int difficulty;
    }
}