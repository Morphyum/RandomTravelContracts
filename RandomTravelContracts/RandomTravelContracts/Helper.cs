using BattleTech;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;

namespace RandomTravelContracts {
    public class Helper {

        public static Settings LoadSettings() {
            try {
                using (StreamReader r = new StreamReader($"{RandomTravelContracts.ModDirectory}/settings.json")) {
                    string json = r.ReadToEnd();
                    return JsonConvert.DeserializeObject<Settings>(json);
                }
            }
            catch (Exception ex) {
                Logger.LogError(ex);
                return null;
            }
        }

        public static bool IsBorder(StarSystem system, SimGameState Sim) {
            try {
                bool result = false;
                if (Sim.Starmap != null ) {
                    foreach (StarSystem neigbourSystem in Sim.Starmap.GetAvailableNeighborSystem(system)) {
                        if (system.Owner != neigbourSystem.Owner) {
                            result = true;
                            break;
                        }
                    }
                }
                return result;
            }
            catch (Exception ex) {
                Logger.LogError(ex);
                return false;
            }
        }

        public static bool IsWarBorder(StarSystem system, SimGameState Sim) {
            try {
                FactionDef factiondef = Sim.FactionsDict[system.Owner];
                bool result = false;
                if (Sim.Starmap != null) {
                    if (system.Owner != Faction.NoFaction) {
                        foreach (StarSystem neigbourSystem in Sim.Starmap.GetAvailableNeighborSystem(system)) {
                            if (factiondef.Enemies.Contains(neigbourSystem.Owner) && neigbourSystem.Owner != Faction.NoFaction) {
                                result = true;
                                break;
                            }
                        }
                    }
                }
                return result;
            }
            catch (Exception ex) {
                Logger.LogError(ex);
                return false;
            }
        }
    }
}