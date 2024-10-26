using System;
using ModuleShared;
using System.Collections.Generic;
using System.Linq;
using FileManagerPlugin;

namespace ServerWipePlugin;

public class PluginMain : AMPPlugin {
    public readonly Settings Settings;
    private readonly ILogger _log;
    private readonly IConfigSerializer _config;
    private readonly IPlatformInfo _platform;
    private readonly IRunningTasksManager _tasks;
    private readonly IApplicationWrapper _application;
    private readonly IPluginMessagePusher _message;
    private readonly IFeatureManager _features;

    public PluginMain(ILogger log, IConfigSerializer config, IPlatformInfo platform,
        IRunningTasksManager taskManager, IApplicationWrapper application, 
        IPluginMessagePusher message, IFeatureManager features) {
        config.SaveMethod = PluginSaveMethod.KVP;
        config.KVPSeparator = "=";
        _log = log;
        _config = config;
        _platform = platform;
        Settings = config.Load<Settings>(AutoSave: true);
        _tasks = taskManager;
        _application = application;
        _message = message;
        _features = features;
        Settings.SettingModified += Settings_SettingModified;
    }

    public override void Init(out WebMethodsBase apiMethods) {
        apiMethods = new WebMethods(this);
    }

    void Settings_SettingModified(object sender, SettingModifiedEventArgs e) {
        _log.Debug($"Setting {e.SettingName} modified");
        // Settings.NotifySettingModified(sender, e);
    }

    public override void PostInit() {}

    public override IEnumerable<SettingStore> SettingStores => Utilities.EnumerableFrom(Settings);

    public ActionResult WipeFile(string path) {
        if (string.IsNullOrWhiteSpace(path)) {
            _log.Error("Path is empty");
            return ActionResult.FailureReason("Path is empty");
        }
        if (path.Contains("..")) {
            _log.Error("Path contains invalid characters");
            return ActionResult.FailureReason("Path contains invalid characters");
        }
        if (path.StartsWith('/')) {
            path = path[1..];
        }
        var fileManager = (IVirtualFileService) _features.RequestFeature<IWSTransferHandler>();
        var file = fileManager.GetFile(path);
        var directory = fileManager.GetDirectory(path);
        if (file.Exists) {
            _log.Debug($"Deleting file {path}");
            file.Delete();
        } else if (directory.Exists) {
            _log.Debug($"Deleting directory {path}");
            directory.Delete();
        } else {
            _log.Debug($"File {path} does not exist, or has already been deleted");
        }
        return ActionResult.Success;
    }
    
    public ActionResult WipeFiles(List<string> paths) {
        ActionResult result = ActionResult.Success;
        foreach (var wipeResult in paths.Select(WipeFile).Where(wipeResult => !wipeResult.Status)) {
            result = wipeResult;
        }
        return result;
    }
    
    public ActionResult WipeServer() {
        _log.Debug("Wiping server");
        var files = Settings.MainSettings.FilesToWipe;
        return WipeFiles(files);
    }
    
    [ScheduleableTask("Wipe a single file/directory")]
    [RequiresPermissions(WebMethods.ServerWipePermissions.WipeFile)]
    public ActionResult ScheduleWipeFile(string path) => WipeFile(path);
    
    [ScheduleableTask("Wipe multiple files/directories")]
    [RequiresPermissions(WebMethods.ServerWipePermissions.WipeFiles)]
    public ActionResult ScheduleWipeFiles(
        [ParameterDescription("Semicolon-separated")] string paths) => WipeFiles(paths.Split(';').ToList());
    
    [RequiresPermissions(WebMethods.ServerWipePermissions.WipeServer)]
    [ScheduleableTask("Wipe the server using the list of files/directories specified in the settings.")]
    public ActionResult ScheduleWipeServer() => WipeServer();
}
