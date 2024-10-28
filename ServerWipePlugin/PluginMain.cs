using System;
using ModuleShared;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using FileManagerPlugin;
using GSMyAdmin;
using Newtonsoft.Json;

namespace ServerWipePlugin;

[AMPDependency("FileManagerPlugin")]
public class PluginMain : AMPPlugin {
    public readonly Settings Settings;
    private readonly ILogger _log;
    private readonly IConfigSerializer _config;
    private readonly IPlatformInfo _platform;
    private readonly IPluginMessagePusher _message;
    private readonly IFeatureManager _features;
    private IVirtualFileService _fileManager;
    public Dictionary<string, List<string>> LocalPresets;
    public Dictionary<string, Dictionary<string, List<string>>> ExternalPresets;
    public static Dictionary<string, Dictionary<string, List<string>>> Presets;
    
    private static readonly HttpClient cl = new();
    private static readonly JsonSerializerSettings serConfig = new() { NullValueHandling = NullValueHandling.Ignore };

    public PluginMain(ILogger log, IConfigSerializer config, IPlatformInfo platform,
        IPluginMessagePusher message, IFeatureManager features) {
        config.SaveMethod = PluginSaveMethod.KVP;
        config.KVPSeparator = "=";
        _log = log;
        _config = config;
        _platform = platform;
        _message = message;
        Settings = config.Load<Settings>(AutoSave: true);
        _features = features;
        Settings.SettingModified += Settings_SettingModified;
    }

    public override void Init(out WebMethodsBase apiMethods) {
        apiMethods = new WebMethods(this);
        LocalPresets = LoadLocalPresets();
        ExternalPresets = LoadExternalPresetsFromCache();
        ConsolidatePresets();
    }

    private void Settings_SettingModified(object sender, SettingModifiedEventArgs e) {
        if (e.NodeName != "ServerWipePlugin.ServerWipe.CurrentPreset") return;
        if ((string) e.NewValue == "None") {
            _log.Debug("Setting current preset to None");
            Settings.ServerWipe.FilesToWipe = [];
            Settings.NotifySettingModified(this, new SettingModifiedEventArgs(() => Settings.ServerWipe.FilesToWipe));
            Settings.ServerWipe.PresetName = "";
            Settings.NotifySettingModified(this, new SettingModifiedEventArgs(() => Settings.ServerWipe.PresetName));
            
            _message.Push("refreshsettings", new Dictionary<string, object> {
                {"ServerWipePlugin.ServerWipe.FilesToWipe", new List<string>()},
                {"ServerWipePlugin.ServerWipe.PresetName", ""}
            });
            return;
        }
        var parts = ((string) e.NewValue).Split(": ");
        foreach (var kvp in Presets) {
            if (kvp.Key != parts[0] || !kvp.Value.TryGetValue(parts[1], out var files)) continue;
            _log.Debug($"Setting current preset to {parts[1]} from {parts[0]}");
            Settings.ServerWipe.FilesToWipe = files;
            Settings.NotifySettingModified(this, new SettingModifiedEventArgs(() => Settings.ServerWipe.FilesToWipe));
            Settings.ServerWipe.PresetName = parts[1];
            Settings.NotifySettingModified(this, new SettingModifiedEventArgs(() => Settings.ServerWipe.PresetName));
            
            _message.Push("refreshsettings", new Dictionary<string, object> {
                {"ServerWipePlugin.ServerWipe.FilesToWipe", files},
                {"ServerWipePlugin.ServerWipe.PresetName", parts[1]}
            });
            return;
        }
    }

    public override bool HasFrontendContent => true;

    public override void PostInit() {
        _fileManager = (IVirtualFileService) _features.RequestFeature<IWSTransferHandler>();
    }

    public override IEnumerable<SettingStore> SettingStores => Utilities.EnumerableFrom(Settings);

    private List<string> GetMatchedPaths(string path) {
        if (!path.Contains('*')) {
            return [path];
        }
        _log.Debug($"Getting matched paths for {path}");
        
        var regex = new Regex("^" + Regex.Escape(path).Replace("\\*", ".*") + "$");
        var dirToSearch = path[..path.IndexOf('*')];
        var dir = dirToSearch[..(dirToSearch.LastIndexOf('/') + 1)];
        return _fileManager.GetDirectoryListing(dir)
            .Where(file => regex.IsMatch(dir + file.Filename))
            .Select(file => dir + file.Filename)
            .ToList();
    }

    private ActionResult WipeFile(string path) {
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
        var result = ActionResult.Success;
        foreach (var path in paths) {
            foreach (var p in GetMatchedPaths(path)) {
                var wipeResult = WipeFile(p);
                if (!wipeResult.Status) {
                    result = wipeResult;
                }
            }
        }
        return result;
    }
    
    public ActionResult WipeServer() {
        _log.Debug("Wiping server");
        var files = Settings.ServerWipe.FilesToWipe;
        return WipeFiles(files);
    }
    
    public ActionResult WipeByPreset(string source, string presetName) {
        _log.Debug($"Wiping server using preset {presetName} from {source}");
        if (string.IsNullOrWhiteSpace(source)) {
            source = "Local";
        }
        if (string.IsNullOrWhiteSpace(presetName)) {
            return ActionResult.FailureReason("Preset name is empty");
        }
        if (!Presets.TryGetValue(source, out var value) ||
            !value.TryGetValue(presetName, out var files)) {
            return ActionResult.FailureReason("Preset not found");
        }
        return WipeFiles(files);
    }

    private const string PresetFile = "wipePresets.json";

    private static Dictionary<string, List<string>> LoadLocalPresets() {
        if (!File.Exists(PresetFile)) {
            return new Dictionary<string, List<string>>();
        }
        var json = File.ReadAllText(PresetFile);
        return JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(json);
    }
    
    public ActionResult SaveLocalPreset(string presetName, List<string> paths) {
        if (string.IsNullOrWhiteSpace(presetName)) {
            presetName = Settings.ServerWipe.PresetName;
        }
        _log.Debug($"Saving local preset {presetName}");
        
        paths ??= Settings.ServerWipe.FilesToWipe;
        LocalPresets[presetName] = paths;
        var json = JsonConvert.SerializeObject(LocalPresets);
        File.WriteAllText(PresetFile, json);
        return ActionResult.Success;
    }
    
    public ActionResult DeleteLocalPreset(string presetName) {
        if (string.IsNullOrWhiteSpace(presetName)) {
            presetName = Settings.ServerWipe.PresetName;
        }
        _log.Debug($"Deleting local preset {presetName}");
        
        LocalPresets.Remove(presetName);
        var json = JsonConvert.SerializeObject(LocalPresets);
        File.WriteAllText(PresetFile, json);
        return ActionResult.Success;
    }
    
    private const string ExternalPresetsCacheFile = "externalPresets.json";

    private void SaveExternalPresets() {
        _log.Debug("Saving external presets");
        var json = JsonConvert.SerializeObject(ExternalPresets);
        File.WriteAllText(ExternalPresetsCacheFile, json);
    }
    
    private static Dictionary<string, Dictionary<string, List<string>>> LoadExternalPresetsFromCache() {
        if (!File.Exists(ExternalPresetsCacheFile)) {
            return new Dictionary<string, Dictionary<string, List<string>>>();
        }
        var json = File.ReadAllText(ExternalPresetsCacheFile);
        return JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, List<string>>>>(json);
    }
    
    private static Dictionary<string, List<string>> FetchExternalPreset(Uri url) {
        var json = cl.GetStringAsync(url).Result;
        return JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(json, serConfig);
    }

    private Dictionary<string, Dictionary<string, List<string>>> FetchExternalPresets() {
        _log.Debug("Fetching external presets...");
        var presets = new Dictionary<string, Dictionary<string, List<string>>>();
        foreach (var (name, url) in Settings.ServerWipe.ExternalPresets) {
            _log.Debug($"Fetching external preset {name} from {url}");
            presets[name] = FetchExternalPreset(new Uri(url));
        }
        return presets;
    }

    private void ConsolidatePresets() {
        _log.Debug("Consolidating presets");
        Presets = ExternalPresets;
        Presets["Local"] = LocalPresets;
    }
    
    public Dictionary<string, Dictionary<string, List<string>>> LoadExternalPresets() {
        _log.Debug("Loading external presets");
        ExternalPresets = FetchExternalPresets();
        SaveExternalPresets();
        ConsolidatePresets();
        return ExternalPresets;
    }

    [ScheduleableTask("Wipe a single file/directory")]
    public ActionResult ScheduleWipeFile(string path)  => WipeFiles([path]);
    
    [ScheduleableTask("Wipe multiple files/directories")]
    public ActionResult ScheduleWipeFiles(
        [ParameterDescription("Semicolon-separated")] string paths) => WipeFiles(paths.Split(';').ToList());
    
    [ScheduleableTask("Wipe the server using the list of files/directories specified in the settings.")]
    public ActionResult ScheduleWipeServer() => WipeServer();
    
    [ScheduleableTask("Wipe the server using a wipe preset.")]
    public ActionResult ScheduleWipeByPreset(
        [ParameterDescription("The source of the preset (\"Local\" by default)")] string source,
        [ParameterDescription("The name of the preset to use")] string presetName) => WipeByPreset(source, presetName);
}
