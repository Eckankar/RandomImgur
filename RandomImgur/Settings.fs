module Settings

open System.Configuration

type SettingType = BooleanSetting | IntegerSetting

let settings = [
    ("Use proxy", "UseProxy", BooleanSetting);
    ("Check for updates", "CheckForUpdates", BooleanSetting);
    ("Number of images", "NumPics", IntegerSetting);
]

type Settings() =
    inherit ApplicationSettingsBase()

    [<UserScopedSettingAttribute()>]
    [<DefaultSettingValueAttribute("false")>]
    member this.useProxy
        with get() = this.Item("UseProxy") :?> bool
        and set(value : bool) = this.Item("UseProxy") <- value

    [<UserScopedSettingAttribute()>]
    [<DefaultSettingValueAttribute("true")>]
    member this.checkForUpdates
        with get() = this.Item("CheckForUpdates") :?> bool
        and set(value : bool) = this.Item("CheckForUpdates") <- value

    [<UserScopedSettingAttribute()>]
    [<DefaultSettingValueAttribute("100")>]
    member this.numPics
        with get() = this.Item("NumPics") :?> int
        and set(value : int) = this.Item("NumPics") <- value