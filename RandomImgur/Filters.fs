module Filters
open System
open System.Net
open System.Text.RegularExpressions

let empty _ = true

let fileType ext =
    let rx = new Regex(@"<link rel=""image_src"" href=""\S*." + ext + @"""\s*/>")
    fun body -> rx.IsMatch body
    
let pViews p =
    let rx = new Regex(@"\s(?:<span id=""views"">)?([\d,]+)(?:</span>)?\s+views")
    fun body -> (rx.Match body).Groups.[1].Value.Replace(",", "") |> int |> p
