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
        WipeServer
    }
    
    [JSONMethod(
        "Wipe a single file or directory.",
        "An ActionResult indicating the success or failure of the operation.")]
    [RequiresPermissions(ServerWipePermissions.WipeFile)]
    public ActionResult WipeFile(
        [ParameterDescription("The path to the file/directory to wipe")] string path) => _plugin.WipeFile(path);
    
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
}
