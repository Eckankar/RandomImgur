module Program
    open System
    open System.Net
    open System.IO
    open System.Drawing
    open System.Windows.Forms
    open System.ComponentModel
    open System.Text

    let NUM_PICTURES = 100
  
    let mainForm = new Form()

    type status = StatusProgress | StatusCancel | StatusComplete | StatusStartup

    type MainForm() as form =
        inherit Form()

        let imagePanel = new FlowLayoutPanel()
        let buttonStrip = new ToolStrip()
        let statusPanel = new Panel()
        let status = new Label()

        let bw = new BackgroundWorker()
       
        let mutable pendingWork = None : (unit -> unit) option
        let imgCounter = ref 0
        let failures = ref 0

        let client = new WebClient()

        do form.initialize

        member this.initialize = 
            form.AutoScaleDimensions <- new System.Drawing.SizeF(1024.0f, 768.0f)
            form.ClientSize <- new System.Drawing.Size(1024, 768)

            imagePanel.Dock <- DockStyle.Fill
            buttonStrip.Dock <- DockStyle.Top
            statusPanel.Dock <- DockStyle.Bottom

            // Populate strip with buttons
            List.iter (fun (label, filter) ->
                let button = new ToolStripButton()
                button.Text <- label
                button.Click.Add (this.buttonClick filter)

                ignore (buttonStrip.Items.Add button)
            ) Imgur.modes

            imagePanel.AutoScroll <- true

            status.AutoSize <- true
            statusPanel.Controls.Add status
            statusPanel.AutoSize <- true

            form.Controls.Add buttonStrip
            form.Controls.Add imagePanel
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
                | StatusProgress -> sprintf "%d of %d images downloaded. (%d%%, %d failures)" !imgCounter NUM_PICTURES (!imgCounter * 100 / NUM_PICTURES) !failures
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
            
                bw.RunWorkerAsync((NUM_PICTURES, filter))
            
            