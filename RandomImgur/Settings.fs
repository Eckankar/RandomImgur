module Settings

open System.Configuration

type Settings() =
    inherit ApplicationSettingsBase()

    [<UserScopedSettingAttribute()>]
    [<DefaultSettingValueAttribute("false")>]
    member this.useProxy
        with get() = this.Item("UseProxy") :?> bool
        and set(value : bool) = this.Item("UseProxy") <- value