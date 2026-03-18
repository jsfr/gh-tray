namespace GhTray

#nowarn "3261"

open System
open System.Diagnostics
open System.Drawing
open System.Reflection
open System.Windows.Forms
open Microsoft.Extensions.Hosting

type TrayApp(lifetime: IHostApplicationLifetime) =
    let contextMenu = new ContextMenuStrip()

    do
        contextMenu.Renderer <-
            { new ToolStripProfessionalRenderer() with
                override _.OnRenderItemText(e) =
                    e.Graphics.TextRenderingHint <- Text.TextRenderingHint.ClearTypeGridFit

                    match e.Item.Tag with
                    | :? (string * string) as (prefix, title) ->
                        let color = e.Item.ForeColor
                        use boldFont = new Font(e.Item.Font, FontStyle.Bold)
                        let prefixSize = TextRenderer.MeasureText(e.Graphics, prefix, boldFont)
                        TextRenderer.DrawText(e.Graphics, prefix, boldFont, e.TextRectangle.Location, color)
                        let titleX = e.TextRectangle.X + prefixSize.Width
                        TextRenderer.DrawText(e.Graphics, title, e.Item.Font, Point(titleX, e.TextRectangle.Y), color)
                    | _ -> base.OnRenderItemText(e) }

    let notifyIcon = new NotifyIcon(Visible = true, ContextMenuStrip = contextMenu)
    let marshalControl = new Control()
    do marshalControl.CreateControl()

    let showContextMenu =
        typeof<NotifyIcon>.GetMethod("ShowContextMenu", BindingFlags.Instance ||| BindingFlags.NonPublic)

    do
        notifyIcon.MouseClick.Add(fun e ->
            if e.Button = MouseButtons.Left then
                showContextMenu.Invoke(notifyIcon, null) |> ignore)

    let mutable lastUpdated: DateTime option = None
    let mutable isStale = false

    let createIcon (text: string) : Icon =
        let size = 32
        let bmp = new Bitmap(size, size)
        use g = Graphics.FromImage(bmp)
        g.SmoothingMode <- Drawing2D.SmoothingMode.AntiAlias
        g.TextRenderingHint <- Text.TextRenderingHint.AntiAliasGridFit
        g.Clear(Color.Transparent)

        use bgBrush = new SolidBrush(Color.FromArgb(60, 60, 60))
        g.FillEllipse(bgBrush, 0, 0, size - 1, size - 1)

        use font = new Font("Segoe UI", 12.0f, FontStyle.Bold)
        use textBrush = new SolidBrush(Color.White)
        let textSize = g.MeasureString(text, font)
        let x = (float32 size - textSize.Width) / 2.0f
        let y = (float32 size - textSize.Height) / 2.0f
        g.DrawString(text, font, textBrush, x, y)

        let icon = Icon.FromHandle(bmp.GetHicon())
        icon

    let statusImage (pr: PullRequest) : Image option =
        match pr.IsDraft, pr.HasConflicts, pr.CheckStatus, pr.ReviewStatus with
        | true, _, _, _ -> Some(EmojiRenderer.render "\U0001F6A7")
        | _, true, _, _ -> Some(EmojiRenderer.render "\u2694\uFE0F")
        | _, _, Some Failure, _ -> Some(EmojiRenderer.render "\u274C")
        | _, _, _, Some ChangesRequested -> Some(EmojiRenderer.render "\U0001F44E")
        | _, _, Some Pending, _ -> Some(EmojiRenderer.render "\u23F3")
        | _, _, _, Some Approved -> Some(EmojiRenderer.render "\U0001F44D")
        | _, _, Some Success, _ -> Some(EmojiRenderer.render "\u2705")
        | _ -> None

    let repoName (nameWithOwner: string) =
        match nameWithOwner.IndexOf('/') with
        | -1 -> nameWithOwner
        | i -> nameWithOwner.Substring(i + 1)

    let addSectionHeader (text: string) =
        let item = new ToolStripLabel(text, IsLink = false)
        item.Font <- new Font(item.Font, FontStyle.Bold)
        item.BackColor <- SystemColors.Control
        contextMenu.Items.Add(item) |> ignore

    let addPrItem (pr: PullRequest) =
        let prefix = $"{repoName pr.Repository}#{pr.Number} "
        let title = pr.Title
        let item = new ToolStripMenuItem(prefix + title)
        item.Tag <- (prefix, title) :> obj

        match statusImage pr with
        | Some img -> item.Image <- img
        | None -> ()

        if pr.IsDraft then
            item.ForeColor <- SystemColors.GrayText

        item.Click.Add(fun _ -> Process.Start(new ProcessStartInfo(pr.Url, UseShellExecute = true)) |> ignore)
        contextMenu.Items.Add(item) |> ignore

    let addSection (header: string) (prs: PullRequest list) =
        match prs with
        | [] -> ()
        | _ ->
            addSectionHeader header
            contextMenu.Items.Add(new ToolStripSeparator()) |> ignore
            prs |> List.iter addPrItem

    let postToUiThread (action: unit -> unit) =
        if marshalControl.InvokeRequired then
            marshalControl.BeginInvoke(Action(action)) |> ignore
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
            let tsItem = new ToolStripLabel($"Last updated: {timestamp}")
            tsItem.ForeColor <- SystemColors.GrayText
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
            marshalControl.Dispose()
