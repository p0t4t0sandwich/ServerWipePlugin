/* global API,UI,PluginHandler */

this.plugin = {
    PreInit: function () {},
    PostInit: function () {},
    Reset: function () {},
    StartupFailure: function () {},
    SettingChanged: async function (node, value) {
        console.log("SettingChanged", node, value);
        if (suppressSettingUpdates) {
            return;
        }
        switch (node) {
            case "ServerWipePlugin.ServerWipe.CurrentPreset":
                let filesToWipe = currentSettings["ServerWipePlugin.ServerWipe.FilesToWipe"];
                await filesToWipe?.category.click();
                let presetName = currentSettings["ServerWipePlugin.ServerWipe.PresetName"];
                await presetName?.category.click();
        }
    },
    AMPDataLoaded: function () {},
    PushedMessage: function(source, message, data) {}
};

this.tabs = [
    {
        File: "wipe_presets.json",
        ExternalTab: true,
        ShortName: "WipePresets",
        Name: "Wipe Presets",
        Icon: "",
        Light: false,
        Category: ""
    }
];

this.stylesheet = "";
