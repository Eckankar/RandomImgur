open Program
open System
open System.Windows.Forms
open System.Reflection
open System.Runtime.CompilerServices
open System.Runtime.InteropServices

[<assembly: AssemblyTitle("Random Imgur")>]
[<assembly: AssemblyDescription("Gets random images from imgur")>]
[<assembly: AssemblyConfiguration("")>]
[<assembly: AssemblyCompany("Sebastian Paaske Tørholm")>]
[<assembly: AssemblyProduct("Random Imgur")>]
[<assembly: AssemblyCopyright("Copyright ©  2012")>]
[<assembly: AssemblyTrademark("")>]

[<assembly: AssemblyVersion("1.0.4.0")>]

[<STAThread>]
do Application.Run(new MainForm())