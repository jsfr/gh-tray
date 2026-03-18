namespace GhTray

#nowarn "3261"

open System
open System.Diagnostics
open System.Drawing
open System.Windows.Forms
open Microsoft.Extensions.Hosting

type TrayApp(lifetime: IHostApplicationLifetime) =
    let contextMenu = new ContextMenuStrip()
    let notifyIcon = new NotifyIcon(Visible = true, ContextMenuStrip = contextMenu)
    let mutable lastUpdated: DateTime option = None
    let mutable isStale = false

    let createIcon (text: string) : Icon =
        let bmp = new Bitmap(16, 16)
        use g = Graphics.FromImage(bmp)
        g.Clear(Color.Transparent)
        use font = new Font("Segoe UI", 7.0f, FontStyle.Bold)
        use brush = new SolidBrush(Color.White)
        let size = g.MeasureString(text, font)
        let x = (16.0f - size.Width) / 2.0f
        let y = (16.0f - size.Height) / 2.0f
        g.DrawString(text, font, brush, x, y)
        Icon.FromHandle(bmp.GetHicon())

    let formatCheckStatus (status: CheckStatus option) =
        match status with
        | Some Success -> "\u2705 "
        | Some Failure -> "\u274C "
        | Some Pending -> "\u23F3 "
        | None -> ""

    let formatReviewStatus (status: ReviewStatus option) =
        match status with
        | Some Approved -> "\U0001F44D "
        | Some ChangesRequested -> "\U0001F44E "
        | Some ReviewRequired -> ""
        | None -> ""

    let formatConflicts (hasConflicts: bool) =
        if hasConflicts then "\u2694\uFE0F " else ""

    let formatDraft (isDraft: bool) =
        if isDraft then "[Draft] " else ""

    let formatPrLabel (pr: PullRequest) =
        let check = formatCheckStatus pr.CheckStatus
        let review = formatReviewStatus pr.ReviewStatus
        let conflicts = formatConflicts pr.HasConflicts
        let draft = formatDraft pr.IsDraft
        $"{check}{review}{conflicts}{pr.Repository}#{pr.Number} - {draft}{pr.Title}"

    let addSectionHeader (text: string) =
        let item = new ToolStripMenuItem(text, Enabled = false)
        item.Font <- new Font(item.Font, FontStyle.Bold)
        contextMenu.Items.Add(item) |> ignore

    let addPrItem (pr: PullRequest) =
        let label = formatPrLabel pr
        let item = new ToolStripMenuItem(label)
        item.Click.Add(fun _ ->
            Process.Start(new ProcessStartInfo(pr.Url, UseShellExecute = true)) |> ignore)
        contextMenu.Items.Add(item) |> ignore

    let addSection (header: string) (prs: PullRequest list) =
        addSectionHeader header
        contextMenu.Items.Add(new ToolStripSeparator()) |> ignore

        match prs with
        | [] ->
            let item = new ToolStripMenuItem("  n/a", Enabled = false)
            contextMenu.Items.Add(item) |> ignore
        | _ -> prs |> List.iter addPrItem

    let postToUiThread (action: unit -> unit) =
        let ctx = System.Threading.SynchronizationContext.Current

        if ctx <> null then
            ctx.Post((fun _ -> action ()), null)
        else if notifyIcon.ContextMenuStrip <> null then
            notifyIcon.ContextMenuStrip.Invoke(Action(action)) |> ignore
        else
            action ()

    member _.SetLoading() =
        notifyIcon.Icon <- createIcon "..."
        notifyIcon.Text <- "gh-tray: loading..."

    member _.Update(group: PullRequestGroup, pollInterval: TimeSpan) =
        let doUpdate () =
            let count = PullRequestGroup.totalCount group
            notifyIcon.Icon <- createIcon (string count)
            notifyIcon.Text <- $"gh-tray: {count} PRs"

            contextMenu.Items.Clear()

            addSection "My PRs" group.Mine
            addSection "Review Requested" group.ReviewRequested
            addSection "Involved" group.Involved

            contextMenu.Items.Add(new ToolStripSeparator()) |> ignore

            let now = DateTime.Now
            lastUpdated <- Some now
            isStale <- false
            let timestamp = now.ToString("HH:mm:ss")
            let tsItem = new ToolStripMenuItem($"Last updated: {timestamp}", Enabled = false)
            contextMenu.Items.Add(tsItem) |> ignore

            contextMenu.Items.Add(new ToolStripSeparator()) |> ignore
            let quitItem = new ToolStripMenuItem("Quit")
            quitItem.Click.Add(fun _ -> lifetime.StopApplication())
            contextMenu.Items.Add(quitItem) |> ignore

        postToUiThread doUpdate

    member _.MarkStale() =
        let doMarkStale () =
            match lastUpdated with
            | Some ts when not isStale ->
                isStale <- true

                for i in 0 .. contextMenu.Items.Count - 1 do
                    let item = contextMenu.Items.[i]

                    if item.Text <> null && item.Text.StartsWith("Last updated:") then
                        let timeStr = ts.ToString("HH:mm:ss")
                        item.Text <- $"\u26A0 Last updated: {timeStr}"
            | _ -> ()

        postToUiThread doMarkStale

    interface IDisposable with
        member _.Dispose() =
            notifyIcon.Visible <- false
            notifyIcon.Dispose()
            contextMenu.Dispose()
