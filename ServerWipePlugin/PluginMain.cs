using System;
using ModuleShared;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FileManagerPlugin;
using GSMyAdmin.WebServer;
using Newtonsoft.Json;

namespace ServerWipePlugin;

[AMPDependency("FileManagerPlugin")]
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "UnusedMember.Global")]
public class PluginMain : AMPPlugin {
    public readonly Settings Settings;
    private readonly ILogger _log;
    [SuppressMessage("ReSharper", "NotAccessedField.Local")]
    private readonly IConfigSerializer _config;
    [SuppressMessage("ReSharper", "NotAccessedField.Local")]
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
    
    public WipePresetGroup LocalPresets;
    public WipePresetCollection ExternalPresets;
    public static WipePresetCollection Presets;
    
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

    public override async Task<WebMethodsBase> InitAsync() {
        WebMethodsBase apiMethods = new WebMethods(this);
        LocalPresets = await LoadLocalPresets();
        ExternalPresets = await LoadExternalPresetsFromCache();
        ConsolidatePresets();
        return apiMethods;
    }

    public override bool HasFrontendContent => true;

    public override Task PostInitAsync() {
        _fileManager = (IVirtualFileService) _features.RequestFeature<IWSTransferHandler>();
        return Task.CompletedTask;
    }

    public override IEnumerable<SettingStore> SettingStores => [Settings];


    private const string SeedSettingNode = "ServerWipePlugin.ServerWipe.SeedSettingNode";
    public void Settings_SettingModified(object sender, SettingModifiedEventArgs e) {
        if (e.NodeName == SeedSettingNode) {
            if (sender is WebMethods or PluginMain) {
                _message.Push("settingValueUpdate",
                    new { SeedSettingNode, newValue = e.NewValue },
                    RequiredPermission: "Settings.ServerWipePlugin.ServerWipe.SeedSettingNode", Source: "ServerWipePlugin");
            } else {
                // Reset the value if it's been modified elsewhere
                Settings.ServerWipe.SeedSettingNode = "";
            }
        }
        
        if (e.NodeName != "ServerWipePlugin.ServerWipe.CurrentPreset") return;
        var settings = new Dictionary<string, object>();
        if ((string) e.NewValue == "None") {
            _log.Info("Setting current preset to None");
            
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
                _log.Info($"Setting current preset to {parts[1]} from {parts[0]}");
            
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
    
    private async Task<List<string>> GetMatchedPaths(string path) {
        if (!path.Contains('*')) {
            return [path];
        }
        _log.Info($"Getting matched paths for {path}");
        
        var regex = new Regex("^" + Regex.Escape(path).Replace("\\*", ".*") + "$");
        var dirToSearch = path[..path.IndexOf('*')];
        var dir = dirToSearch[..(dirToSearch.LastIndexOf('/') + 1)];
        return (await _fileManager.GetDirectoryListing(dir))
            .Where(file => regex.IsMatch(dir + file.Filename))
            .Select(file => dir + file.Filename)
            .ToList();
    }

    private ActionResult WipeFile(string path, bool dryRun) {
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
            _log.Info($"Deleting file {path}");
            if (!dryRun) {
                fileManager.TrashFile(path);
            }
        } else if (directory.Exists) {
            _log.Info($"Deleting directory {path}");
            if (!dryRun) {
                fileManager.TrashDirectory(path);
            }
        } else {
            _log.Info($"File {path} does not exist, or has already been deleted");
        }
        return ActionResult.Success;
    }
    
    public async Task<ActionResult> WipeFiles(List<string> filesToWipe, bool dryRun) {
        var result = ActionResult.Success;
        foreach (var path in filesToWipe) {
            foreach (var p in await GetMatchedPaths(path)) {
                var wipeResult = WipeFile(p, dryRun);
                if (!wipeResult.Status) {
                    result = wipeResult;
                }
            }
        }
        return result;
    }

    private ActionResult SetSeed(WipePreset preset, bool dryRun) {
        if (string.IsNullOrWhiteSpace(preset.SeedSettingNode)) {
            return ActionResult.FailureReason("Seed setting node is empty");
        }
        string seed;
        if (!string.IsNullOrWhiteSpace(preset.SeedSettingValue)) {
            seed = preset.SeedSettingValue;
        } else {
            seed = new Random().Next(preset.SeedSettingMin,
                preset.SeedSettingMax == -1 ? int.MaxValue : preset.SeedSettingMax).ToString();
        }
        _log.Info($"Setting seed setting {preset.SeedSettingNode} to {seed}");
        if (!dryRun) {
            _message.Push("settingValueUpdate", new { preset.SeedSettingNode, newValue = seed },
                RequiredPermission: $"Settings.{preset.SeedSettingNode}", Source: "ServerWipePlugin");
        }
        return ActionResult.Success;
    }
    
    public async Task<ActionResult> WipeServer(bool resetSeed = false, bool dryRun = true) {
        if (dryRun) {
            _log.Info("Dry run enabled");
        }
        _log.Info("Wiping server");
        if (resetSeed || dryRun) {
            var result = SetSeed(Settings.ServerWipe.ToPreset(), dryRun);
            if (!result.Status) {
                return result;
            }
        }
        return await WipeFiles(Settings.ServerWipe.FilesToWipe, dryRun);
    }
    
    public async Task<ActionResult> WipeByPreset(string source, string presetName, bool resetSeed, bool dryRun) {
        if (string.IsNullOrWhiteSpace(source)) {
            source = "Local";
        }
        _log.Info($"Wiping server using preset {presetName} from {source}");
        
        if (string.IsNullOrWhiteSpace(presetName)) {
            return ActionResult.FailureReason("Preset name is empty");
        }
        if (!Presets.TryGetValue(source, out var value) ||
            !value.TryGetValue(presetName, out var preset)) {
            return ActionResult.FailureReason("Preset not found");
        }
        var result = ActionResult.Success;
        if (resetSeed) {
            result = SetSeed(preset, dryRun);
        }
        return result.Status ? await WipeFiles(preset.FilesToWipe, dryRun) : result;
    }

    private const string PresetFile = "wipePresets.json";

    private async Task<WipePresetGroup> LoadLocalPresets() {
        if (!File.Exists(PresetFile)) {
            if (Settings.ServerWipe.InitLocalPresets == "") {
                return new WipePresetGroup();
            }
            var localPresets = JsonConvert.DeserializeObject<WipePresetGroup>(Settings.ServerWipe.InitLocalPresets, serConfig);
            Settings.ServerWipe.InitLocalPresets = "";
            return localPresets;
        }
        var json = await File.ReadAllTextAsync(PresetFile);
        return JsonConvert.DeserializeObject<WipePresetGroup>(json, serConfig);
    }
    
    public async Task<ActionResult> SaveLocalPreset(
        string presetName, List<string> filesToWipe,
        string seedSettingNode, string seedSettingValue,
        int seedSettingMin, int seedSettingMax,
        IWebSession session = null) {
        if (!string.IsNullOrWhiteSpace(seedSettingNode)
            && session != null && !session.HasPermission($"Settings.{seedSettingNode}")) {
            return ActionResult.FailureReason($"This user does not have permissions for the seed setting node, {seedSettingNode}");
        }
        
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
        _log.Info($"Saving local preset {preset.Name}");
        
        LocalPresets[preset.Name] = preset;
        var json = JsonConvert.SerializeObject(LocalPresets, serConfig);
        await File.WriteAllTextAsync(PresetFile, json);
        return ActionResult.Success;
    }
    
    public async Task<ActionResult> DeleteLocalPreset(string presetName) {
        if (string.IsNullOrWhiteSpace(presetName)) {
            presetName = Settings.ServerWipe.PresetName;
        }
        _log.Info($"Deleting local preset {presetName}");
        
        LocalPresets.Remove(presetName);
        var json = JsonConvert.SerializeObject(LocalPresets, serConfig);
        await File.WriteAllTextAsync(PresetFile, json);
        return ActionResult.Success;
    }
    
    private const string ExternalPresetsCacheFile = "externalPresets.json";

    private async Task SaveExternalPresets() {
        _log.Debug("Saving external presets");
        var json = JsonConvert.SerializeObject(ExternalPresets, serConfig);
        await File.WriteAllTextAsync(ExternalPresetsCacheFile, json);
    }
    
    private static async Task<WipePresetCollection> LoadExternalPresetsFromCache() {
        if (!File.Exists(ExternalPresetsCacheFile)) {
            return new WipePresetCollection();
        }
        var json = await File.ReadAllTextAsync(ExternalPresetsCacheFile);
        return JsonConvert.DeserializeObject<WipePresetCollection>(json, serConfig);
    }
    
    private static async Task<WipePresetGroup> FetchExternalPreset(Uri url) {
        var json = await cl.GetStringAsync(url);
        return JsonConvert.DeserializeObject<WipePresetGroup>(json, serConfig);
    }

    private async Task<ActionResult<WipePresetCollection>> FetchExternalPresets(IWebSession session = null) {
        _log.Debug("Fetching external presets...");
        var presets = new WipePresetCollection();
        foreach (var (source, url) in Settings.ServerWipe.ExternalPresets) {
            _log.Debug($"Fetching external preset {source} from {url}");
            var fetched = await FetchExternalPreset(new Uri(url));

            if (session == null) {
                presets[source] = fetched;
                break;
            }

            _log.Debug($"Validating permissions for presets fetched from external source: {source}");
            foreach (var (name, preset) in fetched) {
                if (!string.IsNullOrWhiteSpace(preset.SeedSettingNode)
                    && !session.HasPermission($"Settings.{preset.SeedSettingNode}")) {
                    _log.Warning($"User does not have permission for seed setting node {preset.SeedSettingNode} in Preset: {name}, Source: {source}");
                    return ActionResult<WipePresetCollection>
                        .FailureReason($"This user does not have permissions for the seed setting node, {preset.SeedSettingNode}, from Preset: {name}, Source: {source}");
                }
            }
        }
        return ActionResult<WipePresetCollection>.Success(presets);
    }

    private void ConsolidatePresets() {
        _log.Debug("Consolidating presets");
        Presets = ExternalPresets;
        Presets["Local"] = LocalPresets;
    }
    
    public async Task<WipePresetCollection> LoadExternalPresets(IWebSession session = null) {
        _log.Debug("Loading external presets");
        var presetsToProcess = await FetchExternalPresets(session);
        ExternalPresets = presetsToProcess;
        await SaveExternalPresets();
        ConsolidatePresets();
        return ExternalPresets;
    }
    
    public enum NoYes {
        No,
        Yes
    }
    
    public enum YesNo {
        Yes,
        No
    }

    [ScheduleableTask("Wipe a single file/directory")]
    public async Task<ActionResult> ScheduleWipeFile(
        [ParameterDescription("The path to the file/directory to wipe")] string path,
        [ParameterDescription("Dry run")] NoYes dryRun = NoYes.No
        )  => await WipeFiles([path], dryRun == NoYes.Yes);
    
    [ScheduleableTask("Wipe multiple files/directories")]
    public async Task<ActionResult> ScheduleWipeFiles(
        [ParameterDescription("Semicolon-separated")] string paths,
        [ParameterDescription("Dry run")] NoYes dryRun = NoYes.No
        ) => await WipeFiles(paths.Split(';').ToList(), dryRun == NoYes.Yes);
    
    [ScheduleableTask("Wipe the server using the list of files/directories specified in the settings")]
    public async Task<ActionResult> ScheduleWipeServer(
        [ParameterDescription("Reset the server's seed")] NoYes resetSeed = NoYes.No,
        [ParameterDescription("Dry run")] YesNo dryRun = YesNo.Yes
        ) => await WipeServer(resetSeed == NoYes.Yes, dryRun == YesNo.Yes);
    
    [ScheduleableTask("Wipe the server using a wipe preset")]
    public async Task<ActionResult> ScheduleWipeByPreset(
        [ParameterDescription("The source of the preset (\"Local\" by default)")] string source,
        [ParameterDescription("The name of the preset to use")] string presetName,
        [ParameterDescription("Reset the server's seed")] NoYes resetSeed = NoYes.No,
        [ParameterDescription("Dry run")] YesNo dryRun = YesNo.Yes
        ) => await WipeByPreset(source, presetName, resetSeed == NoYes.Yes, dryRun == YesNo.Yes);
}
