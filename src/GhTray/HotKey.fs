namespace GhTray

open System
open System.Runtime.InteropServices
open System.Windows.Forms

module HotKeyParser =
    [<Literal>]
    let private MOD_ALT = 0x0001u

    [<Literal>]
    let private MOD_CONTROL = 0x0002u

    [<Literal>]
    let private MOD_SHIFT = 0x0004u

    [<Literal>]
    let private MOD_WIN = 0x0008u

    [<Literal>]
    let private MOD_NOREPEAT = 0x4000u

    let tryParse (hotkey: string) : (uint32 * Keys) option =
        let parts =
            hotkey.Split('+')
            |> Array.map (fun s -> s.Trim())
            |> Array.filter (fun s -> s.Length > 0)

        if parts.Length < 2 then
            None
        else
            let mutable modifiers = MOD_NOREPEAT
            let mutable key = Keys.None
            let mutable valid = true

            for i in 0 .. parts.Length - 2 do
                match parts.[i].ToLowerInvariant() with
                | "ctrl"
                | "control" -> modifiers <- modifiers ||| MOD_CONTROL
                | "alt" -> modifiers <- modifiers ||| MOD_ALT
                | "shift" -> modifiers <- modifiers ||| MOD_SHIFT
                | "win"
                | "windows" -> modifiers <- modifiers ||| MOD_WIN
                | _ -> valid <- false

            let keyPart = parts.[parts.Length - 1]

            match Enum.TryParse<Keys>(keyPart, true) with
            | true, k -> key <- k
            | false, _ ->
                if keyPart.Length = 1 && Char.IsLetterOrDigit(keyPart.[0]) then
                    match Enum.TryParse<Keys>(keyPart.ToUpperInvariant(), true) with
                    | true, k -> key <- k
                    | false, _ -> valid <- false
                else
                    valid <- false

            if valid && key <> Keys.None && modifiers <> MOD_NOREPEAT then
                Some(modifiers, key)
            else
                None

module private NativeHotKey =
    [<Literal>]
    let WM_HOTKEY = 0x0312

    [<Literal>]
    let HOTKEY_ID = 1

    [<DllImport("user32.dll", SetLastError = true)>]
    extern bool RegisterHotKey(IntPtr hWnd, int id, uint32 fsModifiers, uint32 vk)

    [<DllImport("user32.dll", SetLastError = true)>]
    extern bool UnregisterHotKey(IntPtr hWnd, int id)

type GlobalHotKey(modifiers: uint32, key: Keys, callback: unit -> unit) =
    inherit NativeWindow()

    let mutable registered = false

    do
        base.CreateHandle(new CreateParams())

        registered <- NativeHotKey.RegisterHotKey(base.Handle, NativeHotKey.HOTKEY_ID, modifiers, uint32 key)

    member _.IsRegistered = registered

    override _.WndProc(m) =
        if m.Msg = NativeHotKey.WM_HOTKEY && int m.WParam = NativeHotKey.HOTKEY_ID then
            callback ()
        else
            base.WndProc(&m)

    interface IDisposable with
        member this.Dispose() =
            if registered then
                NativeHotKey.UnregisterHotKey(this.Handle, NativeHotKey.HOTKEY_ID) |> ignore

                registered <- false

            this.DestroyHandle()
