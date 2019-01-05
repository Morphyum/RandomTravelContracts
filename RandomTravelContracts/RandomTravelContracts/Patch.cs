﻿using BattleTech;
using BattleTech.Data;
using BattleTech.Framework;
using Harmony;
using HBS;
using HBS.Collections;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RandomTravelContracts {
    [HarmonyPatch(typeof(SimGameState), "OnTargetSystemFound")]
    public static class SimGameState_OnTargetSystemFound {

        static bool Prefix(SimGameState __instance) {
            try {
                __instance.CurSystem.RefreshSystem();
                if (__instance.UXAttached) {
                    __instance.RoomManager.ShipRoom.RefreshData();
                }
                __instance.SetReputation(Faction.Owner, __instance.CurSystem.OwnerReputation, StatCollection.StatOperation.Set, null);
                Fields.currBorderCons = 0;
                return false;
            }
            catch (Exception e) {
                Logger.LogError(e);
                return false;
            }
        }
    }

    [HarmonyPatch(typeof(StarSystem), "RefreshBreadcrumbs")]
    public static class StarSystem_RefreshBreadcrumbs {

        static bool Prefix(StarSystem __instance) {
            try {
                if (__instance.CurBreadcrumbOverride > 0) {
                    ReflectionHelper.InvokePrivateMethode(__instance, "set_CurMaxBreadcrumbs", new object[] { __instance.CurBreadcrumbOverride });
                }
                else {
                    int num = __instance.MissionsCompleted;
                    if (num < __instance.Sim.Constants.Story.MissionsForFirstBreadcrumb) {
                        return false;
                    }
                    ReflectionHelper.InvokePrivateMethode(__instance, "set_CurMaxBreadcrumbs", new object[] { __instance.Sim.Constants.Story.MaxBreadcrumbsPerSystem });
                }

                return false;
            }
            catch (Exception e) {
                Logger.LogError(e);
                return false;
            }
        }
    }
    [HarmonyPatch(typeof(StarSystem), "GenerateInitialContracts")]
    public static class StarSystem_GenerateInitialContracts {

        static bool Prefix(StarSystem __instance, Action onContractsFetched = null) {
            try {
                ReflectionHelper.SetPrivateField(__instance, "contractRetrievalCallback", onContractsFetched);

                __instance.Sim.GeneratePotentialContracts(true, null, null, true);

                Action action = (Action)Delegate.CreateDelegate(typeof(Action), __instance, "OnInitialContractFetched");
                List<StarSystem> travels = __instance.Sim.StarSystems;
                travels.Shuffle<StarSystem>();
                __instance.Sim.GeneratePotentialContracts(true, action , travels[0], true);

                return false;
            }
            catch (Exception e) {
                Logger.LogError(e);
                return false;
            }
        }
    }


    [HarmonyPatch(typeof(SimGameState), "GeneratePotentialContracts")]
    public static class SimGameState_GeneratePotentialContracts {

        static bool Prefix(SimGameState __instance, bool clearExistingContracts, Action onContractGenComplete, StarSystem systemOverride = null, bool useCoroutine = false) {
            try {
                LazySingletonBehavior<UnityGameInstance>.Instance.StartCoroutine(StartGeneratePotentialContractsRoutine(__instance, clearExistingContracts, onContractGenComplete, systemOverride, useCoroutine));

                return false;
            }
            catch (Exception e) {
                Logger.LogError(e);
                return false;
            }
        }

        private static IEnumerator StartGeneratePotentialContractsRoutine(SimGameState instance, bool clearExistingContracts, Action onContractGenComplete, StarSystem systemOverride, bool useCoroutine) {
            if (useCoroutine) {
                yield return new WaitForSeconds(0.2f);
            }
            bool usingBreadcrumbs = systemOverride != null;
            StarSystem system;
            List<Contract> contractList;
            if (systemOverride != null) {
                system = systemOverride;
                contractList = instance.CurSystem.SystemBreadcrumbs;
            }
            else {
                system = instance.CurSystem;
                contractList = instance.CurSystem.SystemContracts;
            }
            if (clearExistingContracts) {
                contractList.Clear();
            }
            int maxContracts;
            if (systemOverride != null) {
                maxContracts = instance.CurSystem.CurMaxBreadcrumbs;

            }
            else {
                maxContracts = Mathf.CeilToInt(system.CurMaxContracts);
            }

            int debugCount = 0;
            List<StarSystem> AllSystems = new List<StarSystem>();
            foreach(StarSystem addsystem in instance.StarSystems) {
                if(addsystem.Owner != Faction.NoFaction) {
                    AllSystems.Add(addsystem);
                }
            }
            while (contractList.Count < maxContracts && debugCount < 1000) {
                if (usingBreadcrumbs) {
                    List<StarSystem> listsys = AllSystems;
                    listsys.Shuffle();
                    if (Fields.currBorderCons < maxContracts * Fields.settings.percentageOfTravelOnBorder) {
                        int sysNr = 0;
                        if (Fields.settings.warBorders) {
                            while (!Helper.IsWarBorder(listsys[sysNr], instance)) {
                                sysNr++;
                            }
                        }
                        else {
                            while (!Helper.IsBorder(listsys[sysNr], instance)) {
                                sysNr++;
                            }
                        }
                        system = listsys[sysNr];
                        Fields.currBorderCons++;
                    }
                    else {
                        system = listsys[0];
                    }
                }
                int globalDifficulty = system.Def.GetDifficulty(instance.SimGameMode) + Mathf.FloorToInt(instance.GlobalDifficulty);
                if (globalDifficulty <= 1) {
                    globalDifficulty = 2;
                }
                else if (globalDifficulty > 9) {
                    globalDifficulty = 9;
                }
                int minDiff;
                int maxDiff;

                int contractDifficultyVariance = instance.Constants.Story.ContractDifficultyVariance;
                minDiff = Mathf.Max(1, globalDifficulty - contractDifficultyVariance);
                maxDiff = Mathf.Max(1, globalDifficulty + contractDifficultyVariance);

                ContractDifficulty minDiffClamped = (ContractDifficulty)ReflectionHelper.InvokePrivateMethode(instance, "GetDifficultyEnumFromValue", new object[] { minDiff });
                ContractDifficulty maxDiffClamped = (ContractDifficulty)ReflectionHelper.InvokePrivateMethode(instance, "GetDifficultyEnumFromValue", new object[] { maxDiff });
                WeightedList<MapAndEncounters> contractMaps = new WeightedList<MapAndEncounters>(WeightedListType.SimpleRandom, null, null, 0);
                List<ContractType> contractTypes = new List<ContractType>();
                Dictionary<ContractType, List<ContractOverride>> potentialOverrides = new Dictionary<ContractType, List<ContractOverride>>();
                ContractType[] singlePlayerTypes = (ContractType[])ReflectionHelper.GetPrivateStaticField(typeof(SimGameState), "singlePlayerTypes");
                using (MetadataDatabase metadataDatabase = new MetadataDatabase()) {
                    foreach (Contract_MDD contract_MDD in metadataDatabase.GetContractsByDifficultyRangeAndScopeAndOwnership((int)minDiffClamped, (int)maxDiffClamped, instance.ContractScope, true)) {
                        ContractType contractType = contract_MDD.ContractTypeEntry.ContractType;

                        if (singlePlayerTypes.Contains(contractType)) {
                            if (!contractTypes.Contains(contractType)) {
                                contractTypes.Add(contractType);
                            }
                            if (!potentialOverrides.ContainsKey(contractType)) {
                                potentialOverrides.Add(contractType, new List<ContractOverride>());
                            }
                            ContractOverride item = instance.DataManager.ContractOverrides.Get(contract_MDD.ContractID);
                            potentialOverrides[contractType].Add(item);
                        }
                    }
                    foreach (MapAndEncounters element in metadataDatabase.GetReleasedMapsAndEncountersByContractTypeAndTags(singlePlayerTypes, system.Def.MapRequiredTags, system.Def.MapExcludedTags, system.Def.SupportedBiomes)) {
                        if (!contractMaps.Contains(element)) {
                            contractMaps.Add(element, 0);
                        }
                    }
                }
                if (contractMaps.Count == 0) {
                    Logger.LogLine(string.Format("No valid map for System {0}", system.Name));
                    if (onContractGenComplete != null) {
                        onContractGenComplete();
                    }
                    yield break;
                }
                if (potentialOverrides.Count == 0) {
                    Logger.LogLine(string.Format("No valid contracts queried for difficulties between {0} and {1}, with a SCOPE of {2}", minDiffClamped, maxDiffClamped, instance.ContractScope));
                    if (onContractGenComplete != null) {
                        onContractGenComplete();
                    }
                    yield break;
                }
                contractMaps.Reset(false);
                WeightedList<Faction> validEmployers = new WeightedList<Faction>(WeightedListType.SimpleRandom, null, null, 0);
                Dictionary<Faction, WeightedList<Faction>> validTargets = new Dictionary<Faction, WeightedList<Faction>>();

                Dictionary<Faction, FactionDef> factions = (Dictionary<Faction, FactionDef>)ReflectionHelper.GetPrivateField(instance, "factions");
                foreach (Faction faction in system.Def.ContractEmployers) {
                    foreach (Faction faction2 in factions[faction].Enemies) {
                        if (system.Def.ContractTargets.Contains(faction2)) {
                            if (!validTargets.ContainsKey(faction)) {
                                validTargets.Add(faction, new WeightedList<Faction>(WeightedListType.PureRandom, null, null, 0));
                            }
                            if (!validTargets[faction].Contains(faction2)) {
                                validTargets[faction].Add(faction2, 0);
                            }
                        }
                    }
                    if (validTargets.ContainsKey(faction)) {
                        validTargets[faction].Reset(false);
                        if (!validEmployers.Contains(faction)) {
                            validEmployers.Add(faction, 0);
                        }
                    }
                }
                validEmployers.Reset(false);

                if (validEmployers.Count <= 0 || validTargets.Count <= 0) {
                    Logger.LogLine(string.Format("Cannot find any valid employers or targets for system {0}", system.Name));
                }
                if (validTargets.Count == 0 || validEmployers.Count == 0) {
                    Logger.LogLine(string.Format("There are no valid employers or employers for the system of {0}. Num valid employers: {1}", system.Name, validEmployers.Count));
                    foreach (Faction faction3 in validTargets.Keys) {
                        Logger.LogLine(string.Format("--- Targets for {0}: {1}", faction3, validTargets[faction3].Count));
                    }
                    if (onContractGenComplete != null) {
                        onContractGenComplete();
                    }
                    yield break;
                }

                int i = debugCount;
                debugCount = i + 1;
                WeightedList<MapAndEncounters> activeMaps = new WeightedList<MapAndEncounters>(WeightedListType.SimpleRandom, contractMaps.ToList(), null, 0);
                List<MapAndEncounters> discardedMaps = new List<MapAndEncounters>();

                List<string> mapDiscardPile = (List<string>)ReflectionHelper.GetPrivateField(instance, "mapDiscardPile");

                for (int j = activeMaps.Count - 1; j >= 0; j--) {
                    if (mapDiscardPile.Contains(activeMaps[j].Map.MapID)) {
                        discardedMaps.Add(activeMaps[j]);
                        activeMaps.RemoveAt(j);
                    }
                }
                if (activeMaps.Count == 0) {
                    mapDiscardPile.Clear();
                    foreach (MapAndEncounters element2 in discardedMaps) {
                        activeMaps.Add(element2, 0);
                    }
                }
                activeMaps.Reset(false);
                MapAndEncounters level = null;
                List<EncounterLayer_MDD> validEncounters = new List<EncounterLayer_MDD>();


                Dictionary<ContractType, WeightedList<PotentialContract>> validContracts = new Dictionary<ContractType, WeightedList<PotentialContract>>();
                WeightedList<PotentialContract> flatValidContracts = null;
                do {
                    level = activeMaps.GetNext(false);
                    if (level == null) {
                        break;
                    }
                    validEncounters.Clear();
                    validContracts.Clear();
                    flatValidContracts = new WeightedList<PotentialContract>(WeightedListType.WeightedRandom, null, null, 0);
                    foreach (EncounterLayer_MDD encounterLayer_MDD in level.Encounters) {
                        ContractType contractType2 = encounterLayer_MDD.ContractTypeEntry.ContractType;
                        if (contractTypes.Contains(contractType2)) {
                            if (validContracts.ContainsKey(contractType2)) {
                                validEncounters.Add(encounterLayer_MDD);
                            }
                            else {
                                foreach (ContractOverride contractOverride2 in potentialOverrides[contractType2]) {
                                    bool flag = true;
                                    ContractDifficulty difficultyEnumFromValue = (ContractDifficulty)ReflectionHelper.InvokePrivateMethode(instance, "GetDifficultyEnumFromValue", new object[] { contractOverride2.difficulty });
                                    Faction employer2 = Faction.INVALID_UNSET;
                                    Faction target2 = Faction.INVALID_UNSET;
                                    object[] args = new object[] { system, validEmployers, validTargets, contractOverride2.requirementList, employer2, target2 };
                                    bool validFaction = false;
                                    try {
                                         validFaction = (bool)ReflectionHelper.InvokePrivateMethode(instance, "GetValidFaction", args);
                                    }
                                    catch(Exception e) {
                                        Logger.LogLine("GetValidFaction failed for: "+ contractOverride2.ID);
                                    }    
                                    if (difficultyEnumFromValue >= minDiffClamped && difficultyEnumFromValue <= maxDiffClamped && validFaction) {
                                        employer2 = (Faction)args[4];
                                        target2 = (Faction)args[5];
                                        int difficulty = instance.NetworkRandom.Int(minDiff, maxDiff + 1);
                                        system.SetCurrentContractFactions(employer2, target2);
                                        int k = 0;
                                        while (k < contractOverride2.requirementList.Count) {
                                            RequirementDef requirementDef = new RequirementDef(contractOverride2.requirementList[k]);
                                            EventScope scope = requirementDef.Scope;
                                            TagSet curTags;
                                            StatCollection stats;
                                            switch (scope) {
                                                case EventScope.Company:
                                                    curTags = instance.CompanyTags;
                                                    stats = instance.CompanyStats;
                                                    break;
                                                case EventScope.MechWarrior:
                                                case EventScope.Mech:
                                                    goto IL_88B;
                                                case EventScope.Commander:
                                                    goto IL_8E9;
                                                case EventScope.StarSystem:
                                                    curTags = system.Tags;
                                                    stats = system.Stats;
                                                    break;
                                                default:
                                                    goto IL_88B;
                                            }
                                            IL_803:
                                            for (int l = requirementDef.RequirementComparisons.Count - 1; l >= 0; l--) {
                                                ComparisonDef item2 = requirementDef.RequirementComparisons[l];
                                                if (item2.obj.StartsWith("Target") || item2.obj.StartsWith("Employer")) {
                                                    requirementDef.RequirementComparisons.Remove(item2);
                                                }
                                            }
                                            if (!SimGameState.MeetsRequirements(requirementDef, curTags, stats, null)) {
                                                flag = false;
                                                break;
                                            }
                                            k++;
                                            continue;
                                            IL_88B:
                                            if (scope != EventScope.Map) {
                                                throw new Exception("Contracts cannot use the scope of: " + requirementDef.Scope);
                                            }
                                            using (MetadataDatabase metadataDatabase2 = new MetadataDatabase()) {
                                                curTags = metadataDatabase2.GetTagSetForTagSetEntry(level.Map.TagSetID);
                                                stats = new StatCollection();
                                                goto IL_803;
                                            }
                                            IL_8E9:
                                            curTags = instance.CommanderTags;
                                            stats = instance.CommanderStats;
                                            goto IL_803;
                                        }
                                        if (flag) {
                                            PotentialContract element3 = default(PotentialContract);
                                            element3.contractOverride = contractOverride2;
                                            element3.difficulty = difficulty;
                                            element3.employer = employer2;
                                            element3.target = target2;
                                            validEncounters.Add(encounterLayer_MDD);
                                            if (!validContracts.ContainsKey(contractType2)) {
                                                validContracts.Add(contractType2, new WeightedList<PotentialContract>(WeightedListType.WeightedRandom, null, null, 0));
                                            }
                                            validContracts[contractType2].Add(element3, contractOverride2.weight);
                                            flatValidContracts.Add(element3, contractOverride2.weight);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                while (validContracts.Count == 0 && level != null);
                system.SetCurrentContractFactions(Faction.INVALID_UNSET, Faction.INVALID_UNSET);
                if (validContracts.Count == 0) {
                    if (mapDiscardPile.Count > 0) {
                        mapDiscardPile.Clear();
                    }
                    else {
                        debugCount = 1000;
                        Logger.LogLine(string.Format("[CONTRACT] Unable to find any valid contracts for available map pool. Alert designers.", new object[0]));
                    }
                }
                else {
                    GameContext gameContext = new GameContext(instance.Context);
                    gameContext.SetObject(GameContextObjectTagEnum.TargetStarSystem, system);
                    Dictionary<ContractType, List<EncounterLayer_MDD>> finalEncounters = new Dictionary<ContractType, List<EncounterLayer_MDD>>();
                    foreach (EncounterLayer_MDD encounterLayer_MDD2 in validEncounters) {
                        ContractType contractType3 = encounterLayer_MDD2.ContractTypeEntry.ContractType;
                        if (!finalEncounters.ContainsKey(contractType3)) {
                            finalEncounters.Add(contractType3, new List<EncounterLayer_MDD>());
                        }
                        finalEncounters[contractType3].Add(encounterLayer_MDD2);
                    }
                    List<PotentialContract> discardedContracts = new List<PotentialContract>();
                    List<string> contractDiscardPile = (List<string>)ReflectionHelper.GetPrivateField(instance, "contractDiscardPile");
                    for (int m = flatValidContracts.Count - 1; m >= 0; m--) {
                        if (contractDiscardPile.Contains(flatValidContracts[m].contractOverride.ID)) {
                            discardedContracts.Add(flatValidContracts[m]);
                            flatValidContracts.RemoveAt(m);
                        }
                    }
                    if ((float)discardedContracts.Count >= (float)flatValidContracts.Count * instance.Constants.Story.DiscardPileToActiveRatio || flatValidContracts.Count == 0) {
                        contractDiscardPile.Clear();
                        foreach (PotentialContract element4 in discardedContracts) {
                            flatValidContracts.Add(element4, 0);
                        }
                    }
                    PotentialContract next = flatValidContracts.GetNext(true);
                    ContractType finalContractType = next.contractOverride.contractType;
                    finalEncounters[finalContractType].Shuffle<EncounterLayer_MDD>();
                    string encounterGuid = finalEncounters[finalContractType][0].EncounterLayerGUID;
                    ContractOverride contractOverride3 = next.contractOverride;
                    Faction employer3 = next.employer;
                    Faction target3 = next.target;
                    int targetDifficulty = next.difficulty;
                    Contract con;
                    if (usingBreadcrumbs) {
                        if(employer3 == Faction.Locals) {
                            employer3 = system.Owner;
                            if(target3 == employer3) {
                                target3 = Faction.Locals;
                            }
                        }
                        con = (Contract)ReflectionHelper.InvokePrivateMethode(instance, "CreateTravelContract", new object[] { level.Map.MapName, level.Map.MapPath, encounterGuid, finalContractType, contractOverride3, gameContext, employer3, target3, employer3, false, targetDifficulty });
                    }
                    else {
                        con = new Contract(level.Map.MapName, level.Map.MapPath, encounterGuid, finalContractType, instance.BattleTechGame, contractOverride3, gameContext, true, targetDifficulty, 0, null);
                    }
                    mapDiscardPile.Add(level.Map.MapID);
                    contractDiscardPile.Add(contractOverride3.ID);
                    instance.PrepContract(con, employer3, target3, target3, level.Map.BiomeSkinEntry.BiomeSkin, con.Override.travelSeed, system);
                    contractList.Add(con);
                    if (useCoroutine) {
                        yield return new WaitForSeconds(0.2f);
                    }
                }
            }
            if (debugCount >= 1000) {
                Logger.LogLine("Unable to fill contract list. Please inform AJ Immediately");
            }
            if (onContractGenComplete != null) {
                onContractGenComplete();
            }
            yield break;
        }
    }
}