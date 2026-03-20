namespace GhTray

open System
open System.IO
open Microsoft.Extensions.Logging

type FileLogger(category: string, writer: StreamWriter, minLevel: LogLevel) =
    interface ILogger with
        member _.IsEnabled(logLevel) = logLevel >= minLevel

        member _.Log(logLevel, _eventId, state, ex, formatter) =
            if logLevel >= minLevel then
                let timestamp = DateTimeOffset.Now.ToString "yyyy-MM-dd HH:mm:ss.fff zzz"
                let level = logLevel.ToString().Substring(0, 4).ToUpperInvariant()
                let message = formatter.Invoke(state, ex)

                lock writer (fun () ->
                    writer.WriteLine $"%s{timestamp} [%s{level}] %s{category}: %O{message}"

                    if not (isNull ex) then
                        writer.WriteLine $"  %O{ex}"

                    writer.Flush())

        member _.BeginScope(_state) =
            { new IDisposable with
                member _.Dispose() = () }

type FileLoggerProvider(path: string, minLevel: LogLevel) =
    let writer =
        match Path.GetDirectoryName path |> Option.ofObj with
        | Some dir when dir <> "" -> Directory.CreateDirectory dir |> ignore
        | _ -> ()

        new StreamWriter(path, append = true, AutoFlush = false)

    interface ILoggerProvider with
        member _.CreateLogger(category) = FileLogger(category, writer, minLevel)

        member _.Dispose() = writer.Dispose()
