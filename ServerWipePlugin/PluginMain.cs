using System;
using ModuleShared;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using FileManagerPlugin;
using GSMyAdmin.WebServer;
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

    private GSMyAdmin.WebServer.WebMethods _core;
    private GSMyAdmin.WebServer.WebMethods Core {
        get {
            if (_core != null) {
                return _core;
            }
            var webServer = (LocalWebServer) _features.RequestFeature<ISessionInjector>();
            var field = webServer.GetType().GetField("APImodule", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field == null) {
                _log.Debug("Failed to get APImodule field from LocalWebServer");
                return null;
            }
            var apiService = (ApiService) field.GetValue(webServer);
            if (apiService == null) {
                _log.Debug("Failed to get APImodule from LocalWebServer");
                return null;
            }
            var baseMethodsField = apiService.GetType().GetField("baseMethods", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (baseMethodsField == null) {
                _log.Debug("Failed to get baseMethods field from ApiService");
                return null;
            }
            Core = (GSMyAdmin.WebServer.WebMethods) baseMethodsField.GetValue(apiService);
            if (Core == null) {
                _log.Debug("Failed to get baseMethods from ApiService");
                return null;
            }

            return _core;
        }
        set => _core = value;
    }

    private void SetSettings(Dictionary<string, object> settings) {
        Dictionary<string, string> configs = new();
        foreach (var kvp in settings) {
            configs[kvp.Key] = kvp.Value switch {
                string str => str,
                _ => JsonConvert.SerializeObject(settings[kvp.Key])
            };
            Core.SetConfig(kvp.Key, configs[kvp.Key]);
        }
        _message.Push("setsettings", settings);
    }
    
    public Dictionary<string, WipePreset> LocalPresets;
    public Dictionary<string, Dictionary<string, WipePreset>> ExternalPresets;
    public static Dictionary<string, Dictionary<string, WipePreset>> Presets;
    
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

    public override bool HasFrontendContent => true;

    public override void PostInit() {
        _fileManager = (IVirtualFileService) _features.RequestFeature<IWSTransferHandler>();
    }

    public override IEnumerable<SettingStore> SettingStores => Utilities.EnumerableFrom(Settings);

    private void Settings_SettingModified(object sender, SettingModifiedEventArgs e) {
        if (e.NodeName != "ServerWipePlugin.ServerWipe.CurrentPreset") return;
        var settings = new Dictionary<string, object>();
        if ((string) e.NewValue == "None") {
            _log.Debug("Setting current preset to None");
            
            settings["ServerWipePlugin.ServerWipe.PresetName"] = "";
            settings["ServerWipePlugin.ServerWipe.FilesToWipe"] = new List<string>();
            settings["ServerWipePlugin.ServerWipe.SeedSettingNode"] = "";
            settings["ServerWipePlugin.ServerWipe.SeedSettingValue"] = "";
            settings["ServerWipePlugin.ServerWipe.SeedSettingMin"] = 0;
            settings["ServerWipePlugin.ServerWipe.SeedSettingMax"] = -1;
        } else {
            var parts = ((string) e.NewValue).Split(": ");
            foreach (var kvp in Presets) {
                if (kvp.Key != parts[0] || !kvp.Value.TryGetValue(parts[1], out var preset)) continue;
                _log.Debug($"Setting current preset to {parts[1]} from {parts[0]}");
            
                settings["ServerWipePlugin.ServerWipe.PresetName"] = preset.Name;
                settings["ServerWipePlugin.ServerWipe.FilesToWipe"] = preset.FilesToWipe;
                settings["ServerWipePlugin.ServerWipe.SeedSettingNode"] = preset.SeedSettingNode;
                settings["ServerWipePlugin.ServerWipe.SeedSettingValue"] = preset.SeedSettingValue;
                settings["ServerWipePlugin.ServerWipe.SeedSettingMin"] = preset.SeedSettingMin;
                settings["ServerWipePlugin.ServerWipe.SeedSettingMax"] = preset.SeedSettingMax;
                break;
            }
        }
        SetSettings(settings);
    }
    
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
            fileManager.TrashFile(path);
        } else if (directory.Exists) {
            _log.Debug($"Deleting directory {path}");
            fileManager.TrashDirectory(path);
        } else {
            _log.Debug($"File {path} does not exist, or has already been deleted");
        }
        return ActionResult.Success;
    }
    
    public ActionResult WipeFiles(List<string> filesToWipe) {
        var result = ActionResult.Success;
        foreach (var path in filesToWipe) {
            foreach (var p in GetMatchedPaths(path)) {
                var wipeResult = WipeFile(p);
                if (!wipeResult.Status) {
                    result = wipeResult;
                }
            }
        }
        return result;
    }

    private ActionResult SetSeed(WipePreset preset) {
        if (string.IsNullOrWhiteSpace(preset.SeedSettingNode)) {
            return ActionResult.FailureReason("Seed setting node is empty");
        }
        var settings = new Dictionary<string, object>();
        if (!string.IsNullOrWhiteSpace(preset.SeedSettingValue)) {
            settings[preset.SeedSettingNode] = preset.SeedSettingValue;
        } else {
            var seed = new Random().Next(preset.SeedSettingMin,
                preset.SeedSettingMax == -1 ? int.MaxValue : preset.SeedSettingMax);
            settings[preset.SeedSettingNode] = seed;
        }
        _log.Debug($"Setting seed setting {preset.SeedSettingNode} to {settings[preset.SeedSettingNode]}");
        SetSettings(settings);
        return ActionResult.Success;
    }
    
    public ActionResult WipeServer(bool resetSeed = false) {
        _log.Debug("Wiping server");
        if (resetSeed) {
            var result = SetSeed(Settings.ServerWipe.ToPreset());
            if (!result.Status) {
                return result;
            }
        }
        return WipeFiles(Settings.ServerWipe.FilesToWipe);
    }
    
    public ActionResult WipeByPreset(string source, string presetName, bool resetSeed) {
        if (string.IsNullOrWhiteSpace(source)) {
            source = "Local";
        }
        _log.Debug($"Wiping server using preset {presetName} from {source}");
        
        if (string.IsNullOrWhiteSpace(presetName)) {
            return ActionResult.FailureReason("Preset name is empty");
        }
        if (!Presets.TryGetValue(source, out var value) ||
            !value.TryGetValue(presetName, out var preset)) {
            return ActionResult.FailureReason("Preset not found");
        }
        var result = ActionResult.Success;
        if (resetSeed) {
            result = SetSeed(preset);
        }
        return result.Status ? WipeFiles(preset.FilesToWipe) : result;
    }

    private const string PresetFile = "wipePresets.json";

    private Dictionary<string, WipePreset> LoadLocalPresets() {
        if (!File.Exists(PresetFile)) {
            if (Settings.ServerWipe.InitLocalPresets == "") {
                return new Dictionary<string, WipePreset>();
            }
            var localPresets = JsonConvert.DeserializeObject<Dictionary<string, WipePreset>>(Settings.ServerWipe.InitLocalPresets, serConfig);
            Settings.ServerWipe.InitLocalPresets = "";
            return localPresets;
        }
        var json = File.ReadAllText(PresetFile);
        return JsonConvert.DeserializeObject<Dictionary<string, WipePreset>>(json, serConfig);
    }
    
    public ActionResult SaveLocalPreset(
        string presetName, List<string> filesToWipe,
        string seedSettingNode, string seedSettingValue,
        int seedSettingMin, int seedSettingMax) {
        WipePreset preset;
        if (string.IsNullOrWhiteSpace(presetName)) {
            preset = Settings.ServerWipe.ToPreset();
        } else {
            preset = new WipePreset {
                Name = presetName,
                FilesToWipe = filesToWipe,
                SeedSettingNode = seedSettingNode,
                SeedSettingValue = seedSettingValue,
                SeedSettingMin = seedSettingMin,
                SeedSettingMax = seedSettingMax
            };
        }
        _log.Debug($"Saving local preset {preset.Name}");
        
        LocalPresets[preset.Name] = preset;
        var json = JsonConvert.SerializeObject(LocalPresets, serConfig);
        File.WriteAllText(PresetFile, json);
        return ActionResult.Success;
    }
    
    public ActionResult DeleteLocalPreset(string presetName) {
        if (string.IsNullOrWhiteSpace(presetName)) {
            presetName = Settings.ServerWipe.PresetName;
        }
        _log.Debug($"Deleting local preset {presetName}");
        
        LocalPresets.Remove(presetName);
        var json = JsonConvert.SerializeObject(LocalPresets, serConfig);
        File.WriteAllText(PresetFile, json);
        return ActionResult.Success;
    }
    
    private const string ExternalPresetsCacheFile = "externalPresets.json";

    private void SaveExternalPresets() {
        _log.Debug("Saving external presets");
        var json = JsonConvert.SerializeObject(ExternalPresets, serConfig);
        File.WriteAllText(ExternalPresetsCacheFile, json);
    }
    
    private static Dictionary<string, Dictionary<string, WipePreset>> LoadExternalPresetsFromCache() {
        if (!File.Exists(ExternalPresetsCacheFile)) {
            return new Dictionary<string, Dictionary<string, WipePreset>>();
        }
        var json = File.ReadAllText(ExternalPresetsCacheFile);
        return JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, WipePreset>>>(json, serConfig);
    }
    
    private static Dictionary<string, WipePreset> FetchExternalPreset(Uri url) {
        var json = cl.GetStringAsync(url).Result;
        return JsonConvert.DeserializeObject<Dictionary<string, WipePreset>>(json, serConfig);
    }

    private Dictionary<string, Dictionary<string, WipePreset>> FetchExternalPresets() {
        _log.Debug("Fetching external presets...");
        var presets = new Dictionary<string, Dictionary<string, WipePreset>>();
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
    
    public Dictionary<string, Dictionary<string, WipePreset>> LoadExternalPresets() {
        _log.Debug("Loading external presets");
        ExternalPresets = FetchExternalPresets();
        SaveExternalPresets();
        ConsolidatePresets();
        return ExternalPresets;
    }
    
    public enum NoYes {
        No,
        Yes
    }

    [ScheduleableTask("Wipe a single file/directory")]
    public ActionResult ScheduleWipeFile(string path)  => WipeFiles([path]);
    
    [ScheduleableTask("Wipe multiple files/directories")]
    public ActionResult ScheduleWipeFiles(
        [ParameterDescription("Semicolon-separated")] string paths) => WipeFiles(paths.Split(';').ToList());
    
    [ScheduleableTask("Wipe the server using the list of files/directories specified in the settings.")]
    public ActionResult ScheduleWipeServer([ParameterDescription("Reset the server's seed")] NoYes resetSeed = NoYes.No
    ) => WipeServer(resetSeed == NoYes.Yes);
    
    [ScheduleableTask("Wipe the server using a wipe preset.")]
    public ActionResult ScheduleWipeByPreset(
        [ParameterDescription("The source of the preset (\"Local\" by default)")] string source,
        [ParameterDescription("The name of the preset to use")] string presetName,
        [ParameterDescription("Reset the server's seed")] NoYes resetSeed = NoYes.No
        ) => WipeByPreset(source, presetName, resetSeed == NoYes.Yes);
}
