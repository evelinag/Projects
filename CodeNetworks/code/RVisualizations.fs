(*
This file contains wrapper functions for R for different
visualizations using RProvider.

Requires R.
*)

module RVisualizations

open RProvider
open RProvider.``base``
open RProvider.graphics
open RProvider.grDevices

/// Two main colours for visualization
let colours = [|
    (R.rgb(227,114,34,maxColorValue=255)); 
    (R.rgb(0,179,190,maxColorValue=255))|]
let coloursRList = R.eval(R.parse(text="c(rgb(227,114,34,maxColorValue=255),rgb(0,179,190,maxColorValue=255))"))

/// Plot number of nodes versus number of links
let plotNetworkSizes (csSizes:(int*int)[]) (fsSizes:(int*int)[]) = 
    let csNodeCounts, csLinkCounts = Array.unzip csSizes
    let fsNodeCounts, fsLinkCounts = Array.unzip fsSizes

    // C# values
    R.plot(namedParams [
            "x", box csNodeCounts;
            "y", box csLinkCounts;
            "log", box "xy";
            "bg", box colours.[0];
            "pch", box 21;
            "cex", box 1.7;
            "xlab", box "Number of nodes";
            "ylab", box "Number of links";
            "main", box "Network size";
            "xlim", box [|3.0; 1700.0|];
            "ylim", box [|1.0; 11700.0|]
            ]) |> ignore
    // F# values
    R.points(
        namedParams [
            "x", box fsNodeCounts;
            "y", box fsLinkCounts;
            "log", box "xy"
            "bg", box colours.[1];
            "pch", box 22;
            "cex", box 1.7;
            ]) |> ignore
    // legend
    R.legend( namedParams
        [ "x", box "right";
          "legend", box [|"C#"; "F#"|];
          "col", box coloursRList ;
          "pch", box [|16; 15|];
          "cex", box 1.3;
          "bty", box "n";
        ]) |> ignore

/// Compare diameters with box plots
let plotDiameters (csDiameters:float[]) (fsDiameters:float[]) = 
    let x = 
        let str = 
            "cbind(c(" 
            + (Array.map string csDiameters |> String.concat ",")
            + "),c("
            + (Array.map string fsDiameters |> String.concat ",")
            + "))"
        R.eval(R.parse(text = str))

    namedParams [
        "x", box x;
        "main", box "Network diameter"
        "col", box coloursRList ;
        "cex", box 1.5;
        "names", box ["C#"; "F#"];
        "horizontal", box true
        ]
    |> R.boxplot
    |> ignore

/// Compare percentage of isolated nodes with box plots
let plotIsolatedNodes (csIsolatedPerc:float[]) (fsIsolatedPerc:float[]) = 
    let x = 
        let str = 
            "cbind(c(" 
            + (Array.map string csIsolatedPerc |> String.concat ",")
            + "),c("
            + (Array.map string fsIsolatedPerc |> String.concat ",")
            + "))"
        R.eval(R.parse(text = str))
    namedParams [
        "x", box x;
        "main", box "Isolated nodes (%)"
        "col", box coloursRList;
        "cex", box 1.5;
        "names", box ["C#"; "F#"];
        "horizontal", box true
        ]
    |> R.boxplot
    |> ignore

/// Compare motif profiles of C# and F# projects
let plotMotifComparison (csMotifsAverage:(int*float)[]) (fsMotifAverage:(int*float)[]) = 
    // Create Array2D for motif profiles to pass into R
    let n = csMotifsAverage.Length
    let allMotifs = 
        Array2D.init 2 n (fun language motif ->
            // first row = C#, second row = F#
            if language = 0 then
                snd csMotifsAverage.[motif]
            else
                snd fsMotifAverage.[motif])
    namedParams [
        "height", box allMotifs;
        "beside", box true;
        "ylim", box [|0.0; 0.4|];
        "xlab", box "Motif number";
        "ylab", box "Average motif frequency";
        "names.arg", box ([| 1 .. n |] |> Array.map string);
        "col", box coloursRList;
        ]
    |> R.barplot    
    |> ignore
    // legend
    R.legend( namedParams
        [ "x", box "right";
          "legend", box [|"C#"; "F#"|];
          "col", box coloursRList ;
          "pch", box 15;
          "cex", box 1.3;
          "bty", box "n";
        ]) |> ignore
