using System;
using System.Collections.Generic;

namespace ServerWipePlugin;

[Serializable]
public class WipePreset {
    public string Name = "";
    public List<string> FilesToWipe = [];
}