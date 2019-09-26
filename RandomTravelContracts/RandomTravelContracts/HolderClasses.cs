using BattleTech;
using BattleTech.Framework;

namespace RandomTravelContracts {

    public class Settings {
        public float percentageOfTravelOnBorder = 0.5f;
        public bool warBorders = false;
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

    public class ContractDifficultyRange {
        // Token: 0x0600F658 RID: 63064 RVA: 0x0044BA15 File Offset: 0x00449C15
        public ContractDifficultyRange(int minDiff, int maxDiff, ContractDifficulty minDiffClamped, ContractDifficulty maxDiffClamped) {
            this.MinDifficulty = minDiff;
            this.MinDifficultyClamped = minDiffClamped;
            this.MaxDifficulty = maxDiff;
            this.MaxDifficultyClamped = maxDiffClamped;
        }

        // Token: 0x0400952D RID: 38189
        public int MinDifficulty;

        // Token: 0x0400952E RID: 38190
        public int MaxDifficulty;

        // Token: 0x0400952F RID: 38191
        public ContractDifficulty MinDifficultyClamped;

        // Token: 0x04009530 RID: 38192
        public ContractDifficulty MaxDifficultyClamped;
    }
}