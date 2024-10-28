/* global API,UI,PluginHandler */

this.plugin = {
    PreInit: function () {},
    PostInit: function () {},
    Reset: function () {},
    StartupFailure: function () {},
    SettingChanged: async function (node, value) {},
    AMPDataLoaded: function () {},
    PushedMessage: function(source, message, data) {
        switch (message) {
            case "refreshsettings":
                for (const key in data) {
                    currentSettings[key].value(data[key]);
                }
                break;
        }
    }
};

this.tabs = [];

this.stylesheet = "";
