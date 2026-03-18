namespace GhTray

#nowarn "3261"

open System
open System.Diagnostics
open System.Drawing
open System.Reflection
open System.Windows.Forms
open Microsoft.Extensions.Hosting
open Microsoft.Win32

module private ThemeDetection =
    let isSystemDarkTheme () =
        try
            use key =
                Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")

            match key with
            | null -> false
            | k ->
                match k.GetValue("SystemUsesLightTheme") with
                | :? int as v -> v = 0
                | _ -> false
        with _ ->
            false

module private MenuColors =
    let background isDark =
        if isDark then Color.FromArgb(43, 43, 43) else Color.FromArgb(255, 255, 255)

    let foreground isDark =
        if isDark then Color.White else Color.FromArgb(30, 30, 30)

    let grayText isDark =
        if isDark then Color.FromArgb(153, 153, 153) else Color.FromArgb(109, 109, 109)

    let border isDark =
        if isDark then Color.FromArgb(60, 60, 60) else Color.FromArgb(200, 200, 200)

    let separator isDark =
        if isDark then Color.FromArgb(60, 60, 60) else Color.FromArgb(215, 215, 215)

    let highlight isDark =
        if isDark then Color.FromArgb(65, 65, 65) else Color.FromArgb(229, 243, 255)

    let highlightBorder isDark =
        if isDark then Color.FromArgb(80, 80, 80) else Color.FromArgb(204, 232, 255)

type TrayApp(lifetime: IHostApplicationLifetime) =
    let contextMenu = new ContextMenuStrip()

    let createRenderer (isDark: bool) =
        let colorTable =
            { new ProfessionalColorTable() with
                override _.ToolStripDropDownBackground = MenuColors.background isDark
                override _.MenuBorder = MenuColors.border isDark
                override _.MenuItemBorder = MenuColors.highlightBorder isDark
                override _.MenuItemSelected = MenuColors.highlight isDark
                override _.MenuItemPressedGradientBegin = MenuColors.highlight isDark
                override _.MenuItemPressedGradientEnd = MenuColors.highlight isDark
                override _.SeparatorDark = MenuColors.separator isDark
                override _.SeparatorLight = MenuColors.separator isDark
                override _.ImageMarginGradientBegin = MenuColors.background isDark
                override _.ImageMarginGradientMiddle = MenuColors.background isDark
                override _.ImageMarginGradientEnd = MenuColors.background isDark }

        { new ToolStripProfessionalRenderer(colorTable) with
            override _.OnRenderItemText(e) =
                e.Graphics.TextRenderingHint <- Text.TextRenderingHint.ClearTypeGridFit

                match e.Item.Tag with
                | :? (string * string * bool) as (prefix, title, _) ->
                    let color = e.Item.ForeColor
                    use boldFont = new Font(e.Item.Font, FontStyle.Bold)
                    let prefixSize = TextRenderer.MeasureText(e.Graphics, prefix, boldFont)
                    TextRenderer.DrawText(e.Graphics, prefix, boldFont, e.TextRectangle.Location, color)
                    let titleX = e.TextRectangle.X + prefixSize.Width
                    TextRenderer.DrawText(e.Graphics, title, e.Item.Font, Point(titleX, e.TextRectangle.Y), color)
                | _ -> base.OnRenderItemText(e) }

    let applyMenuTheme (isDark: bool) =
        contextMenu.Renderer <- createRenderer isDark
        contextMenu.BackColor <- MenuColors.background isDark
        contextMenu.ForeColor <- MenuColors.foreground isDark

        for i in 0 .. contextMenu.Items.Count - 1 do
            let item = contextMenu.Items.[i]

            match item with
            | :? ToolStripLabel as lbl when lbl.Font.Bold ->
                lbl.ForeColor <- MenuColors.foreground isDark
            | :? ToolStripLabel as lbl ->
                lbl.ForeColor <- MenuColors.grayText isDark
            | :? ToolStripMenuItem as mi when (match mi.Tag with | :? (string * string * bool) as (_, _, d) -> d | _ -> false) ->
                mi.ForeColor <- MenuColors.grayText isDark
            | :? ToolStripMenuItem as mi ->
                mi.ForeColor <- MenuColors.foreground isDark
            | _ -> ()

    do applyMenuTheme (ThemeDetection.isSystemDarkTheme ())

    let notifyIcon = new NotifyIcon(Visible = true, ContextMenuStrip = contextMenu)
    let marshalControl = new Control()
    do marshalControl.CreateControl()

    let showContextMenu =
        typeof<NotifyIcon>.GetMethod("ShowContextMenu", BindingFlags.Instance ||| BindingFlags.NonPublic)

    let mutable lastUpdated: DateTime option = None
    let mutable isStale = false
    let mutable lastIsDark: bool option = None
    let mutable currentDisplayText: string = "..."

    let createIcon (text: string) (isDark: bool) : Icon =
        let size = 32
        let bmp = new Bitmap(size, size)
        use g = Graphics.FromImage(bmp)
        g.SmoothingMode <- Drawing2D.SmoothingMode.AntiAlias
        g.TextRenderingHint <- Text.TextRenderingHint.AntiAliasGridFit
        g.Clear(Color.Transparent)

        let bgColor =
            if isDark then Color.White else Color.FromArgb(60, 60, 60)

        let textColor =
            if isDark then Color.FromArgb(30, 30, 30) else Color.White

        use bgBrush = new SolidBrush(bgColor)
        g.FillEllipse(bgBrush, 0, 0, size - 1, size - 1)

        use font = new Font("Segoe UI", 12.0f, FontStyle.Bold)
        use textBrush = new SolidBrush(textColor)
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

    let isDark () =
        match lastIsDark with
        | Some d -> d
        | None -> ThemeDetection.isSystemDarkTheme ()

    let addSectionHeader (text: string) =
        let item = new ToolStripLabel(text, IsLink = false)
        item.Font <- new Font(item.Font, FontStyle.Bold)
        item.ForeColor <- MenuColors.foreground (isDark ())
        contextMenu.Items.Add(item) |> ignore

    let addPrItem (pr: PullRequest) =
        let prefix = $"{repoName pr.Repository}#{pr.Number} "
        let title = pr.Title
        let item = new ToolStripMenuItem(prefix + title)
        item.Tag <- (prefix, title, pr.IsDraft) :> obj

        match statusImage pr with
        | Some img -> item.Image <- img
        | None -> ()

        if pr.IsDraft then
            item.ForeColor <- MenuColors.grayText (isDark ())

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

    let currentTheme () =
        let isDark = ThemeDetection.isSystemDarkTheme ()
        lastIsDark <- Some isDark
        isDark

    let refreshIconIfThemeChanged () =
        let isDark = ThemeDetection.isSystemDarkTheme ()

        match lastIsDark with
        | Some d when d = isDark -> ()
        | _ ->
            lastIsDark <- Some isDark
            notifyIcon.Icon <- createIcon currentDisplayText isDark
            applyMenuTheme isDark

    let themeChangedHandler =
        UserPreferenceChangedEventHandler(fun _ e ->
            if e.Category = UserPreferenceCategory.General then
                postToUiThread (fun () -> refreshIconIfThemeChanged ()))

    do
        notifyIcon.MouseClick.Add(fun e ->
            refreshIconIfThemeChanged ()

            if e.Button = MouseButtons.Left then
                showContextMenu.Invoke(notifyIcon, null) |> ignore)

    do SystemEvents.UserPreferenceChanged.AddHandler(themeChangedHandler)

    member _.SetLoading() =
        currentDisplayText <- "..."
        notifyIcon.Icon <- createIcon currentDisplayText (currentTheme ())
        notifyIcon.Text <- "gh-tray: loading..."

    member _.Update(group: PullRequestGroup, pollInterval: TimeSpan) =
        let doUpdate () =
            let count = PullRequestGroup.totalCount group
            currentDisplayText <- string count
            notifyIcon.Icon <- createIcon currentDisplayText (currentTheme ())
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
            tsItem.ForeColor <- MenuColors.grayText (isDark ())
            contextMenu.Items.Add(tsItem) |> ignore

            let autoStartItem = new ToolStripMenuItem("Start with Windows")
            autoStartItem.Checked <- AutoStart.isEnabled ()

            autoStartItem.Click.Add(fun _ ->
                let newState = not autoStartItem.Checked
                AutoStart.setEnabled newState
                autoStartItem.Checked <- newState)

            contextMenu.Items.Add(autoStartItem) |> ignore

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
            SystemEvents.UserPreferenceChanged.RemoveHandler(themeChangedHandler)
            notifyIcon.Visible <- false
            notifyIcon.Dispose()
            contextMenu.Dispose()
            marshalControl.Dispose()
