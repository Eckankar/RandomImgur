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

        let mutable currentNumPics = settings.numPics
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
            
            ignore (configPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize)))
            let proxyCheckbox = new CheckBox()
            proxyCheckbox.Text <- "Use proxy"
            proxyCheckbox.Checked <- settings.useProxy
            proxyCheckbox.CheckedChanged.Add(fun _ -> settings.useProxy <- proxyCheckbox.Checked; settings.Save())
            configPanel.Controls.Add(proxyCheckbox, 0, 0)

            ignore (configPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize)))
            let imageCountLabel = new Label()
            imageCountLabel.Dock <- DockStyle.Fill
            imageCountLabel.TextAlign <- ContentAlignment.MiddleLeft
            imageCountLabel.Text <- "Number of images"
            configPanel.Controls.Add(imageCountLabel, 0, 1)
            let imageCount = new NumericUpDown()
            imageCount.Minimum <- (decimal)1
            imageCount.Maximum <- (decimal)1000000
            imageCount.Value <- (decimal)settings.numPics
            imageCount.Increment <- (decimal)1
            imageCount.Dock <- DockStyle.Fill
            imageCount.DecimalPlaces <- 0
            imageCount.AutoSize <- true
            imageCount.ValueChanged.Add(fun _ -> settings.numPics <- (int)imageCount.Value; settings.Save())           
            configPanel.Controls.Add(imageCount, 1, 1)

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

            bw.RunWorkerCompleted.AddHandler(new RunWorkerCompletedEventHandler(this.bwCompleted))
            bw.DoWork.AddHandler(new DoWorkEventHandler(Imgur.findPictures))
            bw.ProgressChanged.AddHandler(new ProgressChangedEventHandler(this.reportProgress))

            bw.WorkerReportsProgress <- true
            bw.WorkerSupportsCancellation <- true

            this.setStatusText StatusStartup

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
                this.setStatusText StatusProgress
            
                currentNumPics <- settings.numPics
                bw.RunWorkerAsync((settings.numPics, filter))
            
            