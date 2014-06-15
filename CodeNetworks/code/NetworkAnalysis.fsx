#load @"packages\FsLab.0.0.13-beta\FsLab.fsx"
#r @"packages\DotNetZip.1.9.2\lib\net20\Ionic.Zip.dll"
#r @"packages\Mono.Cecil.0.9.5.4\lib\net40\Mono.Cecil.dll"
#r @"packages\Mono.Cecil.0.9.5.4\lib\net40\Mono.Cecil.Rocks.dll"
#r @"packages\QuickGraph.3.6.61119.7\lib\net4\QuickGraph.dll"

#load "projects.fs"
#load "type-dependency-graph.fs"
#load "dependency-network.fs"
#load "NetworkUtils.fs"
#load "downloadNugetPackages.fs"
#load "RVisualizations.fs"

open System
open System.IO
open NetworkUtils
open Projects
open RVisualizations

Directory.SetCurrentDirectory(__SOURCE_DIRECTORY__)

// ===================================================
// Download F# and C# packages from NuGet
// ===================================================

// Download selected packages from NuGet
// Names of projects to download are listed in `projects.fs`
csProjectNames |> Array.iter (FsSnip.NuGet.downloadPackage csDir)
fsProjectNames |> Array.iter (FsSnip.NuGet.downloadPackage fsDir)
    // Storm has to be downloaded separately from CodePlex

// ===================================================
// Extract networks 
// ===================================================

// Extract networks from assemblies and save in JSON format
allProjects
|> Array.iter (fun (projectName, assemblyFile) -> 
    DependencyNetwork.generateNetwork projectName assemblyFile dataDir
    printfn "Finished extracting %s network." projectName)

// Save adjacency matrices for each projects' network
let adjacencyDir = dataDir + "adjacency//"
if not (Directory.Exists adjacencyDir) then Directory.CreateDirectory adjacencyDir |> ignore
allProjectNames
|> Seq.iter (fun project ->
    printfn "%s" project
    saveAdjacencyMatrix dataDir project (adjacencyDir + project + ".adjacency.csv"))

// ================================================
// Basic network statistics
// ================================================

// Size of the networks
let csSizes = csProjectNames |> Array.map networkSize
let fsSizes = fsProjectNames |> Array.map networkSize
plotNetworkSizes csSizes fsSizes
plotNetworkSizesLinear csSizes fsSizes

let allSizes = Array.append csSizes fsSizes
let allSizesCsv = Array.map (fun (n1,n2) -> string n1 + "," + string n2) allSizes
File.WriteAllLines("networks/networkSizes.csv",allSizesCsv)

// Load networks in a format suitable for function in R igraph package
let csNetworks = Array.map projectNetwork csProjectNames
let fsNetworks = Array.map projectNetwork fsProjectNames

// Diameters
let csDiameters = Array.map diameter csNetworks
let fsDiameters = Array.map diameter fsNetworks
plotDiameters csDiameters fsDiameters

// Number of isolated nodes
let csIsolated = Array.map isolatedNodeCount csProjectNames
let fsIsolated = Array.map isolatedNodeCount fsProjectNames
// Percentage of isolated nodes in each network
let networkPercentage count (nodeCount, linkCount) = 
    float count / float nodeCount * 100.0
let csIsolatedPerc = Array.map2 networkPercentage csIsolated csSizes
let fsIsolatedPerc = Array.map2 networkPercentage fsIsolated fsSizes
plotIsolatedNodes csIsolatedPerc fsIsolatedPerc

// Total code size
let csCodeSize = Array.map getTotalCodeSize csProjectNames
let fsCodeSize = Array.map getTotalCodeSize fsProjectNames

// Print overview of network characteristics as Markdown table
let header = 
    [| "| Project | Code size | Number of nodes | Number of links | Isolated nodes | Diameter |";
       "|---------|:---------:|:---------------:|:---------------:|:--------------:|:--------:|" |]
let csTable = 
    csProjectNames
    |> Array.mapi (fun idx name ->
        [| name; string csCodeSize.[idx]; string (fst csSizes.[idx]); string (snd csSizes.[idx]); csIsolatedPerc.[idx].ToString("0.0") + " %"; string csDiameters.[idx] |]
        |> String.concat "|" )
let fsTable = 
    fsProjectNames
    |> Array.mapi (fun idx name ->
        [| name; string fsCodeSize.[idx]; string (fst fsSizes.[idx]); string (snd fsSizes.[idx]); fsIsolatedPerc.[idx].ToString("0.0") + " %"; string fsDiameters.[idx] |]
        |> String.concat "|" )
// C# table
Array.append header csTable |> Array.iter (fun line -> printfn "%s" line)
// F# table
Array.append header fsTable |> Array.iter (fun line -> printfn "%s" line)


// ================================================
// Network motifs
// ================================================

let cs3motifs = motifs 3 csNetworks
let cs4motifs = motifs 4 csNetworks

let fs3motifs = motifs 3 fsNetworks
let fs4motifs = motifs 4 fsNetworks

// Average motif frequencies across the projects, excluding NaNs
let motifAverage motifProfiles = 
    summarizeProfiles motifProfiles
    |> Array.map (fun m -> 
        Array.averageBy (fun x -> snd x) m)
    |> Array.mapi (fun i x -> i,x)
    |> Array.filter (fun (i,x) -> not (Double.IsNaN x))

// Compare average motif frequencies on 3 nodes
let cs3motifsAverage = motifAverage cs3motifs
let fs3motifsAverage = motifAverage fs3motifs
plotMotifComparison cs3motifsAverage fs3motifsAverage

// Most common motifs across all projects
let cs3mostCommonMotifs = cs3motifsAverage |> Array.sortBy snd |> Array.rev
let fs3mostCommonMotifs = fs3motifsAverage |> Array.sortBy snd |> Array.rev

let cs4mostCommonMotifs = cs4motifs |> motifAverage |> Array.sortBy snd |> Array.rev
let fs4mostCommonMotifs = fs4motifs |> motifAverage |> Array.sortBy snd |> Array.rev

// Plot most common motifs
plotMotif 3 (fst cs3mostCommonMotifs.[0])
plotMotif 3 (fst fs3mostCommonMotifs.[0])

// Language-specific motifs
let onlyCs3motifs, onlyFs3motifs = motifOccurences cs3motifs fs3motifs
Array.iter (fun (motif, count) -> 
    printfn "Motif of isomorphism class [%d] occurs in %d C# projects." motif count ) 
    onlyCs3motifs 

let onlyCs4motifs, onlyFs4motifs = motifOccurences cs4motifs fs4motifs
onlyCs4motifs |> Array.sortBy (fun (m,c) -> -c )

// Save raw motif profiles of all projects
exportMotifCounts allProjectNames
    (Array.append csNetworks fsNetworks) 3 "data//motifs_3nodes.csv"
exportMotifCounts allProjectNames 
    (Array.append csNetworks fsNetworks) 4 "data//motifs_4nodes.csv"


// -------------------------------------------------
// Cliques in the graph
open RProvider.igraph
let maxClique = R.maximal_cliques(csNetworks.[0])
let csCliques = csNetworks |> Array.map (R.largest_cliques) 
let fsCliques = fsNetworks |> Array.map (R.largest_cliques)


