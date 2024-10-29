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
