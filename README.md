# ServerWipePlugin

An AMP plugin that can wipe server files with scheduled tasks

## Installation

* Hypothetically, lets call the instance that you're adding this plugin to, `TheInstance01`

1. Download the latest release from the [releases page](https://github.com/p0t4t0sandwich/ServerWipePlugin/releases)
2. Connect to the ADS instance's SFTP server over port 2223, or use the web file manager
3. Extract the contents of the zip file to the `__VDS__ADS01/Plugins` directory in the file manager
    * In a Controller/Hybrid -> Target setup, you'll want to repeat this step for each ADS instance that you'll be using to manage the instance (usually just the main Controller)
    * To elaborate further, there's a JavaScript file in the plugin's `WebRoot` that the ADS needs to have access to, otherwise you can't manage the instance properly.
    * You only need to repeat this when there are updates to the plugin's `WebRoot` files
4. Restart the ADS instance(s) if it's your first time installing the plugin
5. Extract the contents of the zip file to `__VDS__/TheInstance01/Plugins` directory in the file manager
6. Reactivate the instance with your Developer licence key
   * You can get your developer licence key from the [CubeCoders Licence Manager](https://cubecoders.com/account) 
   * On Windows, run the following in CMD: `ampinstmgr reactivate TheInstance01 the-dev-licence-key`
   * On Linux, run `sudo su -l amp` to switch to the `amp` user, then run `ampinstmgr reactivate TheInstance01 the-dev-licence-key`
7. Stop `TheInstance01` and edit it's `__VDS__TheInstance01/AMPConfig.conf` so that `AMP.LoadPlugins=[]` includes `"ServerWipePlugin"`
   * Example config entry: `AMP.LoadPlugins=["ServerWipePlugin"]`
<!-- 7. Run the command `ampinstmgr reconfigure TheInstance01 +Core.AMP.LoadPlugins ServerWipePlugin` so the plugin loads
    * Alternatively stop the instance and edit it's `AMPConfig.conf` so that `AMP.LoadPlugins=[]` includes `"ServerWipePlugin"` -->
8. Start the instance

## **Important Note**

**Keep in mind that if you install this plugin and allow a user to access the scheduler, that you are giving them the
ability to wipe the server's files. Treat Scheduler permissions as you would FileManger permissions.**

## Usage

### Settings

| Setting              | Description                                                                                                                   | Buttons                                                                                                 |
|----------------------|-------------------------------------------------------------------------------------------------------------------------------|---------------------------------------------------------------------------------------------------------|
| `Current Preset`     | A preset of files to wipe (please note, this will override any changes you've made to the current preset)                     | `Refresh List` - Refreshes the list of presets                                                          |
| `Files To Wipe`      | A list of file/directory paths that you'd like to wipe (can be invoked via the scheduler)                                     | `Wipe Now` - Wipes the files immediately, `Dry Run` - Logs what would be without actually deleting them |
| `Preset Name`        | Used when creating new or updating existing presets                                                                           | `Save Preset` - Saves the preset, `Delete Preset` - Deletes the preset                                  |
| `Seed Setting Node`  | The setting to change when setting the seed (enable "Show development information" within the instance to view setting nodes) |                                                                                                         |
| `Seed Setting Value` | The value to set the seed setting to, in cases where you want to hardcode the seed                                            |                                                                                                         |
| `Seed Setting Min`   | The minimum value for the seed setting                                                                                        |                                                                                                         |
| `Seed Setting Max`   | The maximum value for the seed setting (use -1 for no maximum)                                                                |                                                                                                         |
| `External Presets`   | URLs to pull presets from (useful when sharing presets across many instances, eg using a GitHub repo's raw URL)               | `Load Presets` - Loads the presets from the URLs provided                                               |

### Scheduler

| Task                                                                          | Description                                                                    | Parameters                                                                                                                                                                                                            |
|-------------------------------------------------------------------------------|--------------------------------------------------------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Wipe a single file/directory                                                  | Wipes a single file/directory                                                  | `path` - The path to the file/directory to wipe, `dryRun` - Don't actually wipe the files, just log what would be wiped                                                                                               |
| Wipe multiple files/directories                                               | Wipes multiple files/directories                                               | `paths` - A list of paths to the files/directories to wipe (semicolon-separated), `dryRun` - Don't actually wipe the files, just log what would be wiped                                                              |
| Wipe the server using the list of files/directories specified in the settings | Wipes the server using the list of files/directories specified in the settings | `resetSeed` - Reset the server's seed, `dryRun` - Don't actually wipe the files, just log what would be wiped                                                                                                         |
| Wipe the server using a wipe preset                                           | Wipes the server using a wipe preset                                           | `source` - The source of the preset ("Local" by default), `presetName` - The name of the preset to use, `resetSeed` - Reset the server's seed, `dryRun` - Don't actually wipe the files, just log what would be wiped |
