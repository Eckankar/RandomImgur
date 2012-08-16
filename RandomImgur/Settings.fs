module Settings

open System.Configuration

type Settings() =
    inherit ApplicationSettingsBase()

    [<UserScopedSettingAttribute()>]
    [<DefaultSettingValueAttribute("false")>]
    member this.useProxy
        with get() = this.Item("UseProxy") :?> bool
        and set(value : bool) = this.Item("UseProxy") <- value

    [<UserScopedSettingAttribute()>]
    [<DefaultSettingValueAttribute("100")>]
    member this.numPics
        with get() = this.Item("NumPics") :?> int
        and set(value : int) = this.Item("NumPics") <- value