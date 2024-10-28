# ServerWipePlugin

An AMP plugin that can wipe server files with scheduled tasks

## Installation

1. Download the latest release from the [releases page](https://github.com/p0t4t0sandwich/ServerWipePlugin/releases)
2. Connect to the ADS instance's SFTP server over port 2223
3. Extract the contents of the zip file to the `Plugins` directory in your ADS instance
    * In a Controller/Hybrid -> Target setup, you'll want to repeat this step for each ADS instance that you'll be using to manage the instance (usually just the main Controller)
    * To elaborate further, there's a JavaScript file in the plugin's `WebRoot` that the ADS needs to have access to, otherwise you can't manage the instance properly.
    * You only need to repeat this when there are updates to the plugin's `WebRoot` files
4. Restart the ADS instance(s) if it's your first time installing the plugin
5. Extract the contents of the zip file to the `Plugins` directory in the instance that you want to run the plugin on
6. Reactivate the instance with your Developer licence key
7. Start the instance if necessary
