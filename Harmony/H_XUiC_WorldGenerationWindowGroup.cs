using HarmonyLib;


[HarmonyPatch(typeof(XUiC_WorldGenerationWindow), "OnOpen")]
public class H_XUiC_WorldGenerationWindow_OnOpen
{
    public static int defaultOffset = 50;

    public static XUiC_ComboBoxInt terrainOffsetComboBox;

    public static int TerrainOffset => (int)terrainOffsetComboBox.Value;

    public static void Postfix(XUiC_WorldGenerationWindow __instance)
    {
        if (ModManager.ModLoaded("TheDescent"))
        {
            Log.Out("[HigherTerrain] TheDescent is loaded, aborting 'XUiC_WorldGenerationWindow_OnOpen'");
            return;
        }

        terrainOffsetComboBox = __instance.GetChildById("terrainOffset") as XUiC_ComboBoxInt;

        if (terrainOffsetComboBox != null)
        {
            terrainOffsetComboBox.Value = defaultOffset;
        }
    }
}