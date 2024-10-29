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
                // Now actually update the settings
                for (const key in data) {
                    await changeValue(currentSettings[key], data[key]);
                }
                suppressSettingUpdates = false;
                break;
        }
    }
};

this.tabs = [];

this.stylesheet = "";

// Borrowed from AMP.js Line 1537-1582
async function changeValue(self, newValue) {
    let useValue = self.value();

    if (self.settingType.startsWith("Dictionary<")) {
        let kvp = self.value();
        useValue = {};

        for (const element of kvp) {
            useValue[element.Key] = element.Value;
        }
    }

    newValue = self.isComplexType ? JSON.stringify(useValue) : useValue;

    if (self.inputType == "checkbox") {
        self.tooltipClass("tooltiptext tooltiphigher " + (newValue ? "tooltipfarright" : "tooltipright"));
    }

    const result = await API.Core.SetConfigAsync(self.node, newValue);

    if (result.Status === false) {
        self.value(self.oldValue);
    }
    else {
        self.oldValue = useValue;
        PluginHandler.NotifyPluginSettingChanged(self.node, newValue);
    }
}
