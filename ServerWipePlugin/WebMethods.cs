using System.Collections.Generic;
using System.ComponentModel;
using ModuleShared;

namespace ServerWipePlugin;

[DisplayName("ServerWipe")]
class WebMethods : WebMethodsBase
{
    private readonly PluginMain _plugin;

    public WebMethods(PluginMain plugin)
    {
        _plugin = plugin;
    }

    public enum ServerWipePermissions {
        WipeFile,
        WipeFiles,
        WipeServer,
        EditLocalPreset,
        LoadExternalPresets
    }
    
    [JSONMethod(
        "Wipe a single file or directory",
        "An ActionResult indicating the success or failure of the operation")]
    [RequiresPermissions(ServerWipePermissions.WipeFile)]
    public ActionResult WipeFile(
        [ParameterDescription("The path to the file/directory to wipe")] string path,
        [ParameterDescription("Dry run")] bool dryRun = false
        ) => _plugin.WipeFiles([path], dryRun);
    
    [JSONMethod(
        "Wipe the server using the list of files/directories specified in the settings",
        "An ActionResult indicating the success or failure of the operation")]
    [RequiresPermissions(ServerWipePermissions.WipeFiles)]
    public ActionResult WipeFiles(
        [ParameterDescription("The list of files/directory to wipe")] List<string> filesToWipe,
        [ParameterDescription("Dry run")] bool dryRun = false
        ) => _plugin.WipeFiles(filesToWipe, dryRun);

    [JSONMethod(
        "Wipe the server using the list of files/directories specified in the settings",
        "An ActionResult indicating the success or failure of the operation")]
    [RequiresPermissions(ServerWipePermissions.WipeServer)]
    public ActionResult WipeServer([ParameterDescription("Reset the server's seed")] bool resetSeed = false
    ) => _plugin.WipeServer(resetSeed);
    
    [JSONMethod(
        "Wipe the server using the list of files/directories specified in the settings",
        "An ActionResult indicating the success or failure of the operation")]
    [RequiresPermissions(ServerWipePermissions.WipeServer)]
    public ActionResult WipeServerDryRun([ParameterDescription("Reset the server's seed")] bool resetSeed = false
    ) => _plugin.WipeServer(resetSeed);
    
    [JSONMethod(
        "Save a preset of files to wipe",
        "An ActionResult indicating the success or failure of the operation")]
    [RequiresPermissions(ServerWipePermissions.EditLocalPreset)]
    public ActionResult SaveLocalPreset(
        [ParameterDescription("The name of the preset to save")] string presetName = "",
        [ParameterDescription("The list of file paths to store in the preset")] List<string> filesToWipe = null,
        [ParameterDescription("The node in the seed settings to chang")] string seedSettingNode = "",
        [ParameterDescription("The value to set the seed setting to, in cases where you want to hardcode the seed")] string seedSettingValue = "",
        [ParameterDescription("The minimum value for the seed setting")] int seedSettingMin = 0,
        [ParameterDescription("The maximum value for the seed setting (use -1 for no maximum)")] int seedSettingMax = -1
        ) => _plugin.SaveLocalPreset(presetName, filesToWipe, seedSettingNode, seedSettingValue, seedSettingMin, seedSettingMax);
    
    [JSONMethod(
        "Delete a preset of files to wipe",
        "An ActionResult indicating the success or failure of the operation")]
    [RequiresPermissions(ServerWipePermissions.EditLocalPreset)]
    public ActionResult DeleteLocalPreset(
        [ParameterDescription("The name of the preset to delete")] string presetName = ""
        ) => _plugin.DeleteLocalPreset(presetName);

    [JSONMethod(
        "Load a list of presets from an external source",
        "An ActionResult indicating the success or failure of the operation")]
    [RequiresPermissions(ServerWipePermissions.LoadExternalPresets)]
    public Dictionary<string, Dictionary<string, WipePreset>> LoadExternalPresets() => _plugin.LoadExternalPresets();
    
    [JSONMethod(
        "Wipe the server using a wipe preset",
        "An ActionResult indicating the success or failure of the operation")]
    [RequiresPermissions(ServerWipePermissions.WipeServer)]
    public ActionResult WipeByPreset(
        [ParameterDescription("The source of the preset (\"Local\" by default)")] string source,
        [ParameterDescription("The name of the preset to use")] string presetName,
        [ParameterDescription("Reset the server's seed")] bool resetSeed = false,
        [ParameterDescription("Dry run")] bool dryRun = false
        ) => _plugin.WipeByPreset(source, presetName, resetSeed, dryRun);
}
