module Program
    open System
    open System.Net
    open System.IO
    open System.Drawing
    open System.Windows.Forms
    open System.ComponentModel
    open System.Text

    let mainForm = new Form()

    type status = StatusProgress | StatusCancel | StatusComplete | StatusStartup

    type ReportProgressDelegate = delegate of Imgur.progressReport -> unit

    type MainForm() as form =
        inherit Form()

        let imagePanel = new FlowLayoutPanel()
        let buttonStrip = new ToolStrip()
        let statusPanel = new Panel()
        let configContainer = new GroupBox()
        let configPanel = new TableLayoutPanel()
        let centralPanel = new Panel()
        let status = new Label()
        let proxyBtn = new ToolStripButton("Use proxy")
        let settings = new Settings.Settings()

        let mutable reportProgressDelegate = null : ReportProgressDelegate

        let mutable currentNumPics = 0
        let bw = new BackgroundWorker()
       
        let mutable pendingWork = None : (unit -> unit) option
        let imgCounter = ref 0
        let failures = ref 0

        let client = new WebClient()

        do form.initialize

        member this.initialize = 
            form.AutoScaleDimensions <- new System.Drawing.SizeF(1024.0f, 768.0f)
            form.ClientSize <- new System.Drawing.Size(1024, 768)

            centralPanel.Dock <- DockStyle.Fill
            imagePanel.Dock <- DockStyle.Fill
            buttonStrip.Dock <- DockStyle.Top
            configPanel.Dock <- DockStyle.Top
            configContainer.Dock <- DockStyle.Right
            statusPanel.Dock <- DockStyle.Bottom

            // Fix problem where FlowLayoutPanel doesn't render elements after a certain point.
            imagePanel.Scroll.Add(fun _ -> if settings.ImageListFix then imagePanel.PerformLayout() else ())

            // Populate strip with buttons
            List.iter (fun (label, filter) ->
                let button = new ToolStripButton()
                button.Text <- label
                button.Click.Add (this.buttonClick filter)

                ignore (buttonStrip.Items.Add button)
            ) Imgur.modes

            

            ignore (buttonStrip.Items.Add (new ToolStripSeparator()))

            let configButton = new ToolStripButton()
            configButton.Text <- "Options"
            configButton.CheckOnClick <- true
            configButton.Checked <- false
            configButton.CheckedChanged.Add(fun _ -> configContainer.Visible <- configButton.Checked)
            ignore (buttonStrip.Items.Add configButton)

            configPanel.GrowStyle <- TableLayoutPanelGrowStyle.AddRows
            configPanel.AutoSize <- true
                        
            List.iteri (fun curRow (label, name, stype) ->
                ignore (configPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize)))
                match stype with
                    | Settings.BooleanSetting ->
                        let checkbox = new CheckBox()
                        checkbox.Text <- label
                        checkbox.AutoSize <- true
                        checkbox.Checked <- settings.Item(name) :?> bool
                        checkbox.CheckedChanged.Add(fun _ -> settings.Item(name) <- checkbox.Checked; settings.Save())
                        configPanel.Controls.Add(checkbox, 0, curRow)
                        configPanel.SetColumnSpan(checkbox, 2)
                    | Settings.IntegerSetting ->
                        let tlabel = new Label()
                        tlabel.Dock <- DockStyle.Fill
                        tlabel.TextAlign <- ContentAlignment.MiddleLeft
                        tlabel.Text <- label
                        configPanel.Controls.Add(tlabel, 0, curRow)
                        let numUpDown = new NumericUpDown()
                        numUpDown.Minimum <- (decimal)1
                        numUpDown.Maximum <- (decimal)1000000
                        numUpDown.Value <- (decimal) (settings.Item(name) :?> int)
                        numUpDown.Increment <- (decimal)1
                        numUpDown.Dock <- DockStyle.Fill
                        numUpDown.DecimalPlaces <- 0
                        numUpDown.AutoSize <- true
                        numUpDown.ValueChanged.Add(fun _ -> settings.Item(name) <- (int)numUpDown.Value; settings.Save())           
                        configPanel.Controls.Add(numUpDown, 1, curRow)
            ) Settings.settings

            configContainer.Width <- 200
            configContainer.Text <- "Options"
            configContainer.Visible <- false

            imagePanel.AutoScroll <- true
            imagePanel.MouseEnter.Add(fun _ -> ignore (imagePanel.Focus()))

            status.AutoSize <- true
            statusPanel.Controls.Add status
            statusPanel.AutoSize <- true

            configContainer.Controls.Add configPanel

            centralPanel.Controls.Add imagePanel
            centralPanel.Controls.Add configContainer

            form.Controls.Add centralPanel
            form.Controls.Add buttonStrip
            form.Controls.Add statusPanel

            reportProgressDelegate <- new ReportProgressDelegate(this.reportProgressHandler)

            bw.RunWorkerCompleted.AddHandler(new RunWorkerCompletedEventHandler(this.bwCompleted))
            bw.DoWork.AddHandler(new DoWorkEventHandler(Imgur.findPictures))
            bw.ProgressChanged.AddHandler(new ProgressChangedEventHandler(this.reportProgress))

            bw.WorkerReportsProgress <- true
            bw.WorkerSupportsCancellation <- true

            this.setStatusText StatusStartup

            if settings.CheckForUpdates
            then
                let updateWorker = new BackgroundWorker()
                updateWorker.DoWork.AddHandler(new DoWorkEventHandler(fun sender args -> this.checkForUpdates ()))
                updateWorker.RunWorkerAsync ()
            else ()

        member this.checkForUpdates () =
            try
                let webclient = new WebClient()
                if settings.UseProxy then () else webclient.Proxy <- null
                let data = webclient.DownloadString("http://mathemaniac.org/apps/randomimgur/latest-version.txt")
                let latestversion = Version.Parse(data)
                let myversion = Version.Parse(Application.ProductVersion)
                if latestversion > myversion then
                    let res = MessageBox.Show ("A new version (v" + latestversion.ToString() + ") is available for download!\n" +
                                               "Do you wish to go to the download page to download the new version?",
                                               "New version available!",
                                               MessageBoxButtons.YesNo)
                    if res = DialogResult.Yes
                    then this.openUrl(new Uri("http://mathemaniac.org/wp/2012/01/random-imgur-pictures/"))
                    else ()
                else ()
            with
                | ex -> ()

        member this.bwCompleted sender (args : RunWorkerCompletedEventArgs) =
            if args.Cancelled && pendingWork.IsSome then
                pendingWork.Value ()
            else
                this.setStatusText StatusComplete
            
        member this.openUrl (url:Uri) =
            ignore (System.Diagnostics.Process.Start(url.ToString()))

        member this.setStatusText s =
            status.Text <-
              match s with
                | StatusProgress -> sprintf "%d of %d images downloaded. (%d%%, %d failures)" !imgCounter currentNumPics (!imgCounter * 100 / currentNumPics) !failures
                | StatusCancel   -> "Cancelling previous request, please wait..."
                | StatusComplete -> "Completed."
                | StatusStartup  -> "By @pishmoffle (http://mathemaniac.org)"

            form.Text <- "Random imgur v" + Application.ProductVersion + " - " + status.Text

        member this.reportProgress sender (args : ProgressChangedEventArgs) =
            let data = args.UserState :?> Imgur.progressReport
            if this.IsDisposed then ()
            else ignore (this.Invoke(reportProgressDelegate, data))

        member this.reportProgressHandler data =
            match data with
                | Imgur.Failure ->
                    failures := !failures + 1
                | Imgur.Picture (thumb, full) ->
                    try
                        let imgPanel = new Panel()
                        imgPanel.Width <- 150
                        imgPanel.Height <- 150
                        imgPanel.BackgroundImage <- Image.FromStream (thumb)
                        imgPanel.BackgroundImageLayout <- ImageLayout.Stretch
                        imgPanel.Click.Add(fun _ -> this.openUrl full)
                        imgPanel.Cursor <- Cursors.Hand
                        imagePanel.Controls.Add imgPanel            
                        imgCounter := !imgCounter + 1
                    with 
                        | :? ArgumentException ->
                            failures := !failures + 1
                    

            this.setStatusText StatusProgress

        member this.buttonClick filter e =
            if bw.IsBusy then
                bw.CancelAsync()
                pendingWork <- Some (fun () -> this.buttonClick filter e)
                this.setStatusText StatusCancel
            else
                pendingWork <- None
                imgCounter := 0
                failures := 0
                imagePanel.Controls.Clear ()      
                          
                currentNumPics <- settings.NumPics
                this.setStatusText StatusProgress
            
                bw.RunWorkerAsync((settings.NumPics, filter))