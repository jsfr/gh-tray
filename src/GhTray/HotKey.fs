namespace GhTray

open System
open System.Runtime.InteropServices
open System.Windows.Forms

module HotKeyParser =
    [<Literal>]
    let private ModAlt = 0x0001u

    [<Literal>]
    let private ModControl = 0x0002u

    [<Literal>]
    let private ModShift = 0x0004u

    [<Literal>]
    let private ModWin = 0x0008u

    [<Literal>]
    let private ModNorepeat = 0x4000u

    let tryParse (hotkey: string) : (uint32 * Keys) option =
        let parts =
            hotkey.Split '+'
            |> Array.map (fun s -> s.Trim())
            |> Array.filter (fun s -> s.Length > 0)

        if parts.Length < 2 then
            None
        else
            let mutable modifiers = ModNorepeat
            let mutable key = Keys.None
            let mutable valid = true

            for i in 0 .. parts.Length - 2 do
                match parts.[i].ToLowerInvariant() with
                | "ctrl"
                | "control" -> modifiers <- modifiers ||| ModControl
                | "alt" -> modifiers <- modifiers ||| ModAlt
                | "shift" -> modifiers <- modifiers ||| ModShift
                | "win"
                | "windows" -> modifiers <- modifiers ||| ModWin
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

            if valid && key <> Keys.None && modifiers <> ModNorepeat then
                Some(modifiers, key)
            else
                None

module private NativeHotKey =
    [<Literal>]
    let WmHotkey = 0x0312

    [<Literal>]
    let HotkeyId = 1

    [<DllImport("user32.dll", SetLastError = true)>]
    extern bool RegisterHotKey(IntPtr _hWnd, int _id, uint32 _fsModifiers, uint32 _vk)

    [<DllImport("user32.dll", SetLastError = true)>]
    extern bool UnregisterHotKey(IntPtr _hWnd, int _id)

type GlobalHotKey(modifiers: uint32, key: Keys, callback: unit -> unit) =
    inherit NativeWindow()

    let mutable registered = false

    do
        base.CreateHandle(CreateParams())

        registered <- NativeHotKey.RegisterHotKey(base.Handle, NativeHotKey.HotkeyId, modifiers, uint32 key)

    member _.IsRegistered = registered

    override _.WndProc m =
        if m.Msg = NativeHotKey.WmHotkey && int m.WParam = NativeHotKey.HotkeyId then
            callback ()
        else
            base.WndProc(&m)

    interface IDisposable with
        member this.Dispose() =
            if registered then
                NativeHotKey.UnregisterHotKey(this.Handle, NativeHotKey.HotkeyId) |> ignore

                registered <- false

            this.DestroyHandle()
