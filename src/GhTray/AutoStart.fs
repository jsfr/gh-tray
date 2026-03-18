namespace GhTray

#nowarn "3261"

open System
open Microsoft.Win32

module AutoStart =
    let private keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run"
    let private valueName = "GhTray"

    let isEnabled () =
        try
            use key = Registry.CurrentUser.OpenSubKey(keyPath)

            match key with
            | null -> false
            | k -> k.GetValue(valueName) <> null
        with _ ->
            false

    let setEnabled (enabled: bool) =
        try
            use key = Registry.CurrentUser.OpenSubKey(keyPath, true)

            match key with
            | null -> ()
            | k ->
                if enabled then
                    let exePath = Environment.ProcessPath
                    k.SetValue(valueName, exePath)
                else
                    k.DeleteValue(valueName, false)
        with _ ->
            ()
