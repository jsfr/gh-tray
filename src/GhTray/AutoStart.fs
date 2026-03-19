namespace GhTray


open System
open Microsoft.Win32

module AutoStart =
    let private keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run"
    let private valueName = "GhTray"

    let isEnabled () =
        try
            use key = Registry.CurrentUser.OpenSubKey keyPath

            match key with
            | null -> false
            | k -> not (isNull (k.GetValue valueName))
        with _ ->
            false

    let setEnabled (enabled: bool) =
        try
            use key = Registry.CurrentUser.OpenSubKey(keyPath, true)

            match key with
            | null -> ()
            | k ->
                if enabled then
                    match Environment.ProcessPath |> Option.ofObj with
                    | Some exePath -> k.SetValue(valueName, exePath)
                    | None -> ()
                else
                    k.DeleteValue(valueName, false)
        with _ ->
            ()
