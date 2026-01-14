using System.Collections;
using System.Reflection;
using WorldGenerationEngineFinal;
using HarmonyLib;
using Unity.Collections;


[HarmonyPatch(typeof(WorldBuilder), "GenerateData")]
public static class H_WorldBuilder_GenerateData
{
    private static int TerrainOffset => H_XUiC_WorldGenerationWindowGroup_OnOpen.TerrainOffset;

    private static WorldBuilder worldBuilder;

    public static bool Prefix(WorldBuilder __instance, ref IEnumerator __result)
    {
        if (ModManager.ModLoaded("TheDescent"))
        {
            Log.Out("[HigherTerrain] TheDescent is loaded, aborting 'WorldBuilder_GenerateData'");
            return true;
        }

        worldBuilder = __instance;
        __result = GenerateData();

        return false;
    }

    public static IEnumerator GenerateData()
    {
        PatchWaterHeight();

        yield return worldBuilder.Init();
        yield return worldBuilder.SetMessage(string.Format(Localization.Get("xuiWorldGenerationGenerating"), worldBuilder.WorldName), _logToConsole: true);
        yield return worldBuilder.GenerateTerrain();

        if (worldBuilder.IsCanceled)
            yield break;

        worldBuilder.InitStreetTiles();

        if (worldBuilder.IsCanceled)
            yield break;

        bool hasPOIs = worldBuilder.Towns != 0 || worldBuilder.Wilderness != WorldBuilder.GenerationSelections.None;
        if (hasPOIs)
        {
            yield return worldBuilder.PrefabManager.LoadPrefabs();
            worldBuilder.PrefabManager.ShufflePrefabData(worldBuilder.Seed);
            yield return null;
            worldBuilder.PathingUtils.SetupPathingGrid();
        }
        else
        {
            worldBuilder.PrefabManager.ClearDisplayed();
        }

        StoreHeightMaps(out var heightMap, out var waterDest);
        PatchHeightMaps();

        if (worldBuilder.Towns != 0)
        {
            yield return worldBuilder.TownPlanner.Plan(worldBuilder.thisWorldProperties, worldBuilder.Seed);
        }

        ResetHeightMaps(heightMap, waterDest);

        yield return worldBuilder.GenerateTerrainLast();

        PatchHeightMaps();

        if (worldBuilder.IsCanceled)
            yield break;

        yield return worldBuilder.POISmoother.SmoothStreetTiles();

        if (worldBuilder.IsCanceled)
            yield break;

        if (worldBuilder.Wilderness != 0)
        {
            yield return worldBuilder.WildernessPlanner.Plan(worldBuilder.thisWorldProperties, worldBuilder.Seed);
            yield return worldBuilder.SmoothWildernessTerrain();

            if (worldBuilder.IsCanceled)
            {
                yield break;
            }
        }
        if (hasPOIs)
        {
            worldBuilder.CalcTownshipsHeightMask();
            yield return worldBuilder.HighwayPlanner.Plan(worldBuilder.thisWorldProperties, worldBuilder.Seed);
            yield return worldBuilder.TownPlanner.SpawnPrefabs();
            if (worldBuilder.IsCanceled)
            {
                yield break;
            }
        }

        if (worldBuilder.Wilderness != 0)
        {
            yield return worldBuilder.WildernessPathPlanner.Plan(worldBuilder.Seed);
        }
        int num = 12 - worldBuilder.playerSpawns.Count;
        if (num > 0)
        {
            foreach (StreetTile item in worldBuilder.CalcPlayerSpawnTiles())
            {
                if (worldBuilder.CreatePlayerSpawn(item.WorldPositionCenter, _isFallback: true) && --num <= 0)
                {
                    break;
                }
            }
        }

        yield return GCUtils.UnloadAndCollectCo();
        yield return worldBuilder.SetMessage(Localization.Get("xuiRwgDrawRoads"), _logToConsole: true);
        yield return worldBuilder.DrawRoads(worldBuilder.roadDest);

        if (hasPOIs)
        {
            yield return worldBuilder.SetMessage(Localization.Get("xuiRwgSmoothRoadTerrain"), _logToConsole: true);
            worldBuilder.CalcWindernessPOIsHeightMask(worldBuilder.roadDest);
            yield return worldBuilder.SmoothRoadTerrain(worldBuilder.roadDest, worldBuilder.data.HeightMap, worldBuilder.WorldSize, worldBuilder.Townships);
        }

        foreach (Path highwayPath in worldBuilder.highwayPaths)
        {
            highwayPath.Cleanup();
        }

        foreach (Path wildernessPath in worldBuilder.wildernessPaths)
        {
            wildernessPath.Cleanup();
        }

        worldBuilder.highwayPaths.Clear();
        worldBuilder.wildernessPaths.Clear();

        yield return worldBuilder.FinalizeWater();
        yield return worldBuilder.SerializeData();
        yield return GCUtils.UnloadAndCollectCo();

        Log.Out("RWG final in {0}:{1:00}, r={2:x}", worldBuilder.totalMS.Elapsed.Minutes, worldBuilder.totalMS.Elapsed.Seconds, Rand.Instance.PeekSample());

        yield break;
    }

    private static float ClampHeight(float height)
    {
        return TerrainOffset + (255f - TerrainOffset) * height / 255f;
    }

    private static void PatchWaterHeight()
    {
        SetField<WorldBuilder>(
            worldBuilder,
            "WaterHeight",
            (int)ClampHeight(worldBuilder.WaterHeight)
        );
    }

    private static void PatchHeightMaps()
    {
        for (int i = 0; i < worldBuilder.data.HeightMap.Length; i++)
        {
            worldBuilder.data.HeightMap[i] = ClampHeight(worldBuilder.data.HeightMap[i]);
        }
    }

    private static void StoreHeightMaps(out NativeArray<float> HeightMap, out NativeArray<float> waterDest)
    {
        HeightMap = new NativeArray<float>(worldBuilder.data.HeightMap.Length, Allocator.Persistent);
        waterDest = new NativeArray<float>(worldBuilder.data.waterDest.Length, Allocator.Persistent);

        worldBuilder.data.HeightMap.CopyTo(HeightMap);
        worldBuilder.data.waterDest.CopyTo(waterDest);
    }

    private static void ResetHeightMaps(NativeArray<float> HeightMap, NativeArray<float> waterDest)
    {
        worldBuilder.data.HeightMap.CopyFrom(HeightMap);
        worldBuilder.data.waterDest.CopyFrom(waterDest);
    }

    private static void SetField<T>(object instance, string fieldName, object value)
    {
        var field = typeof(T).GetField(fieldName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
        field.SetValue(instance, value);
    }
}