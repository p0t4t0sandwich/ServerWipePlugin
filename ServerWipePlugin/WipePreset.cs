using System;
using System.Collections.Generic;

namespace ServerWipePlugin;

[Serializable]
public class WipePreset {
    public string Name = "";
    public List<string> FilesToWipe = [];
    public string SeedSettingNode = "";
    public string SeedSettingValue = "";
    public int SeedSettingMin = 0;
    public int SeedSettingMax = -1;
}

[Serializable]
public class WipePresetGroup : Dictionary<string, WipePreset>;

[Serializable]
public class WipePresetCollection : Dictionary<string, WipePresetGroup>;
