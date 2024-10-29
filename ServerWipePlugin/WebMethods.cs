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
        "Wipe a single file or directory.",
        "An ActionResult indicating the success or failure of the operation.")]
    [RequiresPermissions(ServerWipePermissions.WipeFile)]
    public ActionResult WipeFile(
        [ParameterDescription("The path to the file/directory to wipe")] string path) => _plugin.WipeFiles([path]);
    
    [JSONMethod(
        "Wipe the server using the list of files/directories specified in the settings.",
        "An ActionResult indicating the success or failure of the operation.")]
    [RequiresPermissions(ServerWipePermissions.WipeFiles)]
    public ActionResult WipeFiles(
        [ParameterDescription("The list of files/directory to wipe")] List<string> paths) => _plugin.WipeFiles(paths);

    [JSONMethod(
        "Wipe the server using the list of files/directories specified in the settings.",
        "An ActionResult indicating the success or failure of the operation.")]
    [RequiresPermissions(ServerWipePermissions.WipeServer)]
    public ActionResult WipeServer() => _plugin.WipeServer();
    
    [JSONMethod(
        "Save a preset of files to wipe.",
        "An ActionResult indicating the success or failure of the operation.")]
    [RequiresPermissions(ServerWipePermissions.EditLocalPreset)]
    public ActionResult SaveLocalPreset(
        [ParameterDescription("The name of the preset to save")] string presetName = "",
        [ParameterDescription("The list of file paths to store in the preset")] List<string> paths = null
        ) => _plugin.SaveLocalPreset(presetName, paths);
    
    [JSONMethod(
        "Delete a preset of files to wipe.",
        "An ActionResult indicating the success or failure of the operation.")]
    [RequiresPermissions(ServerWipePermissions.EditLocalPreset)]
    public ActionResult DeleteLocalPreset(
        [ParameterDescription("The name of the preset to delete")] string presetName = ""
        ) => _plugin.DeleteLocalPreset(presetName);

    [JSONMethod(
        "Load a list of presets from an external source.",
        "An ActionResult indicating the success or failure of the operation.")]
    [RequiresPermissions(ServerWipePermissions.LoadExternalPresets)]
    public Dictionary<string, Dictionary<string, WipePreset>> LoadExternalPresets() => _plugin.LoadExternalPresets();
    
    [JSONMethod(
        "Wipe the server using a wipe preset.",
        "An ActionResult indicating the success or failure of the operation.")]
    [RequiresPermissions(ServerWipePermissions.WipeServer)]
    public ActionResult WipeByPreset(
        [ParameterDescription("The source of the preset (\"Local\" by default)")] string source,
        [ParameterDescription("The name of the preset to use")] string presetName
        ) => _plugin.WipeByPreset(source, presetName);
}
