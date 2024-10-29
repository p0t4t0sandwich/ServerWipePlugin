using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ModuleShared;

namespace ServerWipePlugin;

[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
[SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Global")]
public class Settings : SettingStore {
    public ServerWipeSettings ServerWipe = new();
    
    [SuppressMessage("ReSharper", "CollectionNeverUpdated.Global")]
    [SuppressMessage("ReSharper", "UnassignedField.Global")]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public class ServerWipeSettings : SettingSectionStore {
        [WebSetting("Files to Wipe", "A list of file/directory paths that you'd like to wipe (can be invoked via the scheduler)", false)]
        [InlineAction("ServerWipePlugin", "WipeServer", "Wipe Now")]
        public List<string> FilesToWipe = [];
        
        [WebSetting("Current Preset", "A preset of files to wipe (please note, this will override any changes you've made to the current preset)", false)]
        [StringSelectionSource(typeof (ServerWipeSettings), "LoadPresets")]
        [InlineAction("Core", "RefreshSettingValueList", "Refresh List", "ServerWipePlugin.ServerWipe.CurrentPreset")]
        public string CurrentPreset;
        
        [SuppressMessage("ReSharper", "UnusedParameter.Global")]
        public static IEnumerable<string> LoadPresets(object context, IApplicationWrapper app) {
            var list = new List<string> {"None"};
            list.AddRange(from kvp in PluginMain.Presets from vk in kvp.Value.Keys select kvp.Key + ": " + vk);
            return list;
        }
        
        [WebSetting("Preset Name", "Used when creating new presets", false)]
        [InlineAction("ServerWipePlugin", "SaveLocalPreset", "Save Preset")]
        [InlineAction("ServerWipePlugin", "DeleteLocalPreset", "Delete Preset")]
        public string PresetName;
        
        [WebSetting("Seed Setting Node", "The node in the seed settings to change", false)]
        public string SeedSettingNode = "";
        
        [WebSetting("Seed Setting Value", "The value to set the seed setting to, in cases where you want to hardcode the seed", false)]
        public string SeedSettingValue = "";
        
        [WebSetting("Seed Setting Min", "The minimum value for the seed setting", false)]
        public int SeedSettingMin = 0;
        
        [WebSetting("Seed Setting Max", "The maximum value for the seed setting (use -1 for no maximum)", false)]
        public int SeedSettingMax = -1;
        
        [WebSetting("External Presets", "URLs to pull presets from (useful when sharing presets across many instances, eg using a GitHub repo's raw URL)", false)]
        [DictionaryExtendedInfo("Source Name", "URL", "Some Nifty Preset", "https://raw.githubusercontent.com/someuser/somerepo/refs/heads/main/somefile.json")]
        [InlineAction("ServerWipePlugin", "LoadExternalPresets", "Load Presets")]
        public Dictionary<string, string> ExternalPresets = new();
        
        public WipePreset ToPreset() {
            return new WipePreset {
                Name = PresetName,
                FilesToWipe = FilesToWipe,
                SeedSettingNode = SeedSettingNode,
                SeedSettingValue = SeedSettingValue,
                SeedSettingMin = SeedSettingMin,
                SeedSettingMax = SeedSettingMax
            };
        }
    }
}
