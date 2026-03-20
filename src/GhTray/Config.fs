namespace GhTray

open System
open System.IO
open System.Text.Json
open System.Text.Json.Serialization

type AppConfig =
    { Account: string option
      PollInterval: int
      LogLevel: string
      Hotkey: string }

module Config =
    let defaultConfig =
        { Account = None
          PollInterval = 120
          LogLevel = "Information"
          Hotkey = "Ctrl+Alt+Shift+G" }

    type private ConfigFile =
        { Account: string option
          PollInterval: int option
          LogLevel: string option
          Hotkey: string option }

    let private options =
        JsonFSharpOptions
            .Default()
            .WithSkippableOptionFields()
            .ToJsonSerializerOptions(PropertyNameCaseInsensitive = true)

    let private fromFile (cf: ConfigFile) : AppConfig =
        { Account = cf.Account
          PollInterval = cf.PollInterval |> Option.defaultValue defaultConfig.PollInterval
          LogLevel = cf.LogLevel |> Option.defaultValue defaultConfig.LogLevel
          Hotkey = cf.Hotkey |> Option.defaultValue defaultConfig.Hotkey }

    let configPath () =
        let appData = Environment.GetFolderPath Environment.SpecialFolder.ApplicationData
        Path.Combine(appData, "gh-tray", "config.json")

    let load () =
        let path = configPath ()

        if File.Exists path then
            try
                let json = File.ReadAllText path

                JsonSerializer.Deserialize<ConfigFile>(json, options)
                |> Option.ofObj
                |> Option.map fromFile
                |> Option.defaultValue defaultConfig
            with ex ->
                eprintfn "Failed to parse config file %s: %s" path ex.Message
                defaultConfig
        else
            defaultConfig
