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
        [RelatedSetting("CurrentPreset")]
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
        [RelatedSetting("CurrentPreset")]
        public string PresetName;
        
        [WebSetting("External Presets", "URLs to pull presets from (useful when sharing presets across many instances, eg using a GitHub repo's raw URL)", false)]
        [DictionaryExtendedInfo("Source Name", "URL", "Some Nifty Preset", "https://raw.githubusercontent.com/someuser/somerepo/refs/heads/main/somefile.json")]
        [InlineAction("ServerWipePlugin", "LoadExternalPresets", "Load Presets")]
        public Dictionary<string, string> ExternalPresets = new();
    }
}
