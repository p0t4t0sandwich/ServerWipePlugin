using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
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

    [PermissionNodeName("ServerWipe")]
    public enum ServerWipePermissions {
        WipeFile,
        WipeFiles,
        WipeServer,
        EditLocalPreset,
        LoadExternalPresets
    }

    [JSONMethod(
        "Set the setting node to be used for resetting the server's seed during a wipe",
        "An ActionResult indicating the success or failure of the operation")]
    [RequiresPermissions("ServerWipePlugin.ServerWipe.SeedSettingNode")]
    public ActionResult SetSeedSettingNode(
        IWebSession session,
        [ParameterDescription("The node for the seed setting")] string seedSettingNode = "") {
        if (string.IsNullOrWhiteSpace(seedSettingNode)) {
            seedSettingNode = _plugin.Settings.ServerWipe.SeedSettingNodeHolder;
        }

        if (!session.HasPermission($"Settings.{seedSettingNode}")) {
            return ActionResult.FailureReason($"Current user does not have permission to modify the setting node \"{seedSettingNode}\"");
        }

        SettingModifiedEventArgs e = new SettingModifiedEventArgs(
            "ServerWipePlugin.ServerWipe.SeedSettingNode", 
            "Seed Setting Node", seedSettingNode);
        _plugin.Settings_SettingModified(this, e);

        return ActionResult.Success;
    }

    [JSONMethod(
        "Wipe a single file or directory",
        "An ActionResult indicating the success or failure of the operation")]
    [RequiresPermissions(
        ServerWipePermissions.WipeFile,
        "FileManager.FileManager.TrashFiles",
        "FileManager.FileManager.TrashDirectories")]
    public async Task<ActionResult> WipeFile(
        [ParameterDescription("The path to the file/directory to wipe")] string path,
        [ParameterDescription("Dry run")] bool dryRun = false
        ) => await _plugin.WipeFiles([path], dryRun);
    
    [JSONMethod(
        "Wipe the server using the list of files/directories specified in the settings",
        "An ActionResult indicating the success or failure of the operation")]
    [RequiresPermissions(
        ServerWipePermissions.WipeFiles,
        "FileManager.FileManager.TrashFiles",
        "FileManager.FileManager.TrashDirectories")]
    public async Task<ActionResult> WipeFiles(
        [ParameterDescription("The list of files/directory to wipe")] List<string> filesToWipe,
        [ParameterDescription("Dry run")] bool dryRun = false
        ) => await _plugin.WipeFiles(filesToWipe, dryRun);

    [JSONMethod(
        "Wipe the server using the list of files/directories specified in the settings",
        "An ActionResult indicating the success or failure of the operation")]
    [RequiresPermissions(
        ServerWipePermissions.WipeServer,
        "FileManager.FileManager.TrashFiles",
        "FileManager.FileManager.TrashDirectories")]
    public async Task<ActionResult> WipeServer([ParameterDescription("Reset the server's seed")] bool resetSeed = false
    ) => await _plugin.WipeServer(resetSeed, false);
    
    [JSONMethod(
        "Wipe the server using the list of files/directories specified in the settings",
        "An ActionResult indicating the success or failure of the operation")]
    [RequiresPermissions(
        ServerWipePermissions.WipeServer,
        "FileManager.FileManager.TrashFiles",
        "FileManager.FileManager.TrashDirectories")]
    public async Task<ActionResult> WipeServerDryRun([ParameterDescription("Reset the server's seed")] bool resetSeed = false
    ) => await _plugin.WipeServer(resetSeed);
    
    [JSONMethod(
        "Save a preset of files to wipe",
        "An ActionResult indicating the success or failure of the operation")]
    [RequiresPermissions(ServerWipePermissions.EditLocalPreset)]
    public async Task<ActionResult> SaveLocalPreset(
        IWebSession session,
        [ParameterDescription("The name of the preset to save")] string presetName = "",
        [ParameterDescription("The list of file paths to store in the preset")] List<string> filesToWipe = null,
        [ParameterDescription("The node for the seed setting")] string seedSettingNode = "",
        [ParameterDescription("The value to set the seed setting to, in cases where you want to hardcode the seed")] string seedSettingValue = "",
        [ParameterDescription("The minimum value for the seed setting")] int seedSettingMin = 0,
        [ParameterDescription("The maximum value for the seed setting (use -1 for no maximum)")] int seedSettingMax = -1
        ) => await _plugin.SaveLocalPreset(presetName, filesToWipe, seedSettingNode, seedSettingValue, seedSettingMin, seedSettingMax, session);
    
    [JSONMethod(
        "Delete a preset of files to wipe",
        "An ActionResult indicating the success or failure of the operation")]
    [RequiresPermissions(ServerWipePermissions.EditLocalPreset)]
    public async Task<ActionResult> DeleteLocalPreset(
        [ParameterDescription("The name of the preset to delete")] string presetName = ""
        ) => await _plugin.DeleteLocalPreset(presetName);

    [JSONMethod(
        "Load a list of presets from an external source",
        "An ActionResult indicating the success or failure of the operation")]
    [RequiresPermissions(ServerWipePermissions.LoadExternalPresets)]
    public async Task<ActionResult<WipePresetCollection>> LoadExternalPresets(IWebSession session)
        => await _plugin.LoadExternalPresets(session);
    
    [JSONMethod(
        "Wipe the server using a wipe preset",
        "An ActionResult indicating the success or failure of the operation")]
    [RequiresPermissions(
        ServerWipePermissions.WipeServer,
        "FileManager.FileManager.TrashFiles",
        "FileManager.FileManager.TrashDirectories")]
    public async Task<ActionResult> WipeByPreset(
        [ParameterDescription("The source of the preset (\"Local\" by default)")] string source,
        [ParameterDescription("The name of the preset to use")] string presetName,
        [ParameterDescription("Reset the server's seed")] bool resetSeed = false,
        [ParameterDescription("Dry run")] bool dryRun = false
        ) => await _plugin.WipeByPreset(source, presetName, resetSeed, dryRun);
}
