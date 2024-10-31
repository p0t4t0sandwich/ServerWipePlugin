/* global API,UI,PluginHandler */

this.plugin = {
    PreInit: function () {},
    PostInit: function () {},
    Reset: function () {},
    StartupFailure: function () {},
    SettingChanged: async function (node, value) {},
    AMPDataLoaded: function () {},
    PushedMessage: async function(source, message, data) {
        switch (message) {
            case "setsettings":
                suppressSettingUpdates = true;
                // Get that sweet sweet instant visual feedback
                for (const key in data) {
                    currentSettings[key].value(data[key]);
                }
                suppressSettingUpdates = false;
                break;
        }
    }
};

this.tabs = [];

this.stylesheet = "";
