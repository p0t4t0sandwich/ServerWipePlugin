using System.Collections.Generic;
using ModuleShared;

namespace ServerWipePlugin;

public class Settings : SettingStore {
    public ServerWipeSettings MainSettings = new();
    
    public class ServerWipeSettings : SettingSectionStore {
        [WebSetting("Files to Wipe", "A list of file/directory paths that you'd like to wipe (can be invoked via the scheduler)", false)]
        [InlineAction("ServerWipePlugin", "WipeServer", "Wipe Now")]
        public List<string> FilesToWipe = [];
    }

    public enum WipePreset {
        Minecraft,
    }
    
    public Dictionary<WipePreset, List<string>> Presets = new() {
        {WipePreset.Minecraft, new List<string> {
            
        }}
    };
}
