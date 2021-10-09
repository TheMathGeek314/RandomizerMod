﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ItemChanger;
using SD = ItemChanger.Util.SceneDataUtil;
using RandomizerMod.RandomizerData;
using RandomizerMod.Settings;
using static RandomizerMod.LogHelper;

namespace RandomizerMod.IC
{
    public static class Export
    {
        public static void BeginExport()
        {
            ItemChangerMod.CreateSettingsProfile(overwrite: true);
            ItemChangerMod.Modules.Add<RandomizerModule>();
            ItemChangerMod.Modules.Add<TrackerUpdate>();
            ItemChangerMod.Modules.Add<TrackerLog>();
        }


        public static void ExportStart(GenerationSettings gs)
        {
            string startName = gs.StartLocationSettings.StartLocation;
            if (!string.IsNullOrEmpty(startName) && Data.GetStartDef(startName) is RandomizerData.StartDef def)
            {
                ItemChangerMod.ChangeStartGame(new ItemChanger.StartDef
                {
                    SceneName = def.sceneName,
                    X = def.x,
                    Y = def.y,
                    MapZone = (int)def.zone,
                });
            }

            foreach (SmallPlatform p in PlatformList.GetPlatformList(gs)) ItemChangerMod.AddDeployer(p); 

            switch (startName)
            {
                // Platforms to allow escaping the Hive start regardless of difficulty or initial items
                case "Hive":
                    ItemChangerMod.AddDeployer(new SmallPlatform { SceneName = SceneNames.Hive_03, X = 58.5f, Y = 134f, });
                    ItemChangerMod.AddDeployer(new SmallPlatform { SceneName = SceneNames.Hive_03, X = 58.5f, Y = 138.5f, });
                    break;

                // Drop the vine platforms and add small platforms for jumping up.
                case "Far Greenpath":
                    ItemChangerMod.AddDeployer(new SmallPlatform { SceneName = SceneNames.Fungus1_13, X = 45f, Y = 16.5f });
                    ItemChangerMod.AddDeployer(new SmallPlatform { SceneName = SceneNames.Fungus1_13, X = 64f, Y = 16.5f });
                    SD.Save(SceneNames.Fungus1_13, "Vine Platform (1)");
                    SD.Save(SceneNames.Fungus1_13, "Vine Platform (2)");
                    break;

                // With the Lower Greenpath start, getting to the rest of Greenpath requires
                // cutting the vine to the right of the vessel fragment.
                case "Lower Greenpath":
                    if (gs.NoveltySettings.RandomizeNail) SD.Save(SceneNames.Fungus1_13, "Vine Platform");
                    break;
            }
        }

        public static void ExportItemPlacements(GenerationSettings gs, IReadOnlyList<RandomizerCore.ItemPlacement> randoPlacements)
        {
            DefaultShopItems defaultShopItems = GetDefaultShopItems(gs);
            Dictionary<string, AbstractPlacement> export = new();
            for(int j = 0; j < randoPlacements.Count; j++)
            {
                var (item, location) = randoPlacements[j];
                if (!export.TryGetValue(location.Name, out AbstractPlacement p))
                {
                    var l = Finder.GetLocation(location.Name);
                    if (l != null)
                    {
                        p = l.Wrap();
                    }
                    else if (location.Name == "Grubfather")
                    {
                        var chest = Finder.GetLocation(LocationNames.Grubberflys_Elegy) as ItemChanger.Locations.ContainerLocation;
                        var tablet = Finder.GetLocation(LocationNames.Mask_Shard_5_Grubs) as ItemChanger.Locations.PlaceableLocation;
                        if (chest == null || tablet == null)
                        {
                            Log("Error constructing Grubfather location!");
                            continue;
                        }

                        chest.name = tablet.name = "Grubfather";
                        p = new ItemChanger.Placements.CostChestPlacement("Grubfather")
                        {
                            chestLocation = chest,
                            tabletLocation = tablet,
                        };
                    }
                    else if (location.Name == "Seer")
                    {
                        var chest = Finder.GetLocation(LocationNames.Awoken_Dream_Nail) as ItemChanger.Locations.ContainerLocation;
                        var tablet = Finder.GetLocation(LocationNames.Hallownest_Seal_Seer) as ItemChanger.Locations.PlaceableLocation;
                        if (chest == null || tablet == null)
                        {
                            Log("Error constructing Grubfather location!");
                            continue;
                        }

                        chest.name = tablet.name = "Seer";
                        p = new ItemChanger.Placements.CostChestPlacement("Seer")
                        {
                            chestLocation = chest,
                            tabletLocation = tablet,
                        };
                    }
                    else
                    {
                        throw new ArgumentException($"Location {location.Name} did not correspond to any ItemChanger location!");
                    }

                    if (location.costs != null && p is ItemChanger.Placements.ISingleCostPlacement scp)
                    {
                        scp.Cost = CostConversion.Convert(location.costs); // we assume all items for the scp have the same cost, and only apply it once.
                    }
                    if (p is ItemChanger.Placements.ShopPlacement sp) sp.defaultShopItems = defaultShopItems;
                    p.AddTag<RandoPlacementTag>();
                    export.Add(p.Name, p);
                }

                var i = Finder.GetItem(item.Name);
                if (i == null)
                {
                    throw new ArgumentException($"Item {item.Name} did not correspond to any ItemChanger item!");
                }
                if (item.Name == "Split_Shade_Cloak" && !((RC.SplitCloakItem)item.item).LeftBiased) // default is left biased
                {
                    i.GetTag<ItemChanger.Tags.ItemTreeTag>().predecessors = new string[] { ItemNames.Right_Mothwing_Cloak, ItemNames.Left_Mothwing_Cloak };
                }

                if (location.costs != null)
                {
                    if (p is ItemChanger.Placements.IMultiCostPlacement)
                    {
                        i.GetOrAddTag<CostTag>().Cost += CostConversion.Convert(location.costs);
                    }
                    else if (p is not ItemChanger.Placements.ISingleCostPlacement)
                    {
                        if (p.Name == "Dash_Slash") { }
                        else if (p.Name.Contains("Map")) { }
                        else throw new InvalidOperationException($"Attached cost {location.costs[0]} to placement {p.Name} which does not support costs!");
                    }
                }

                i.AddTag<RandoItemTag>().id = j;
                p.Add(i);
            }

            ItemChangerMod.AddPlacements(export.Select(kvp => kvp.Value));
        }

        public static void ExportTransitionPlacements(IEnumerable<RandomizerCore.TransitionPlacement> ps)
        {
            foreach (var p in ps) ItemChangerMod.AddTransitionOverride(new Transition(p.source.lt.sceneName, p.source.lt.gateName), new Transition(p.target.lt.sceneName, p.target.lt.gateName));
        }

        public static DefaultShopItems GetDefaultShopItems(GenerationSettings gs)
        {
            DefaultShopItems items = DefaultShopItems.None;

            items |= DefaultShopItems.IseldaMapPins;
            items |= DefaultShopItems.IseldaMapMarkers;
            items |= DefaultShopItems.SalubraBlessing;

            if (!gs.PoolSettings.Keys)
            {
                items |= DefaultShopItems.SlyLantern;
                items |= DefaultShopItems.SlySimpleKey;
                items |= DefaultShopItems.SlyKeyElegantKey;
            }

            if (!gs.PoolSettings.Charms)
            {
                items |= DefaultShopItems.SlyCharms;
                items |= DefaultShopItems.SlyKeyCharms;
                items |= DefaultShopItems.IseldaCharms;
                items |= DefaultShopItems.SalubraCharms;
                items |= DefaultShopItems.LegEaterCharms;
                items |= DefaultShopItems.LegEaterRepair;
            }

            if (!gs.PoolSettings.Maps)
            {
                items |= DefaultShopItems.IseldaQuill;
                items |= DefaultShopItems.IseldaMaps;
            }

            if (!gs.PoolSettings.MaskShards)
            {
                items |= DefaultShopItems.SlyMaskShards;
            }

            if (!gs.PoolSettings.VesselFragments)
            {
                items |= DefaultShopItems.SlyVesselFragments;
            }

            if (!gs.PoolSettings.RancidEggs)
            {
                items |= DefaultShopItems.SlyRancidEgg;
            }

            return items;
        }
    }
}