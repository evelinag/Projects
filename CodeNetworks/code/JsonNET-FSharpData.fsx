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
let dataDir = Path.Combine(__SOURCE_DIRECTORY__, "../JsonNet_vs_FSharpData")

//==================================
FsSnip.NuGet.downloadPackage dataDir "Newtonsoft.Json"
FsSnip.NuGet.downloadPackage dataDir "FSharp.Data"
let jsonNet = dataDir + "/Newtonsoft.Json/lib/net40/Newtonsoft.Json.dll"
let fsharpData = dataDir + "/FSharp.Data/lib/net40/FSharp.Data.dll"


// ===================================================
// Extract networks with Mono.Cecil
DependencyNetwork.generateNetwork "Json.NET" jsonNet dataDir
DependencyNetwork.generateNetwork "FSharp.Data" fsharpData dataDir

// ===================================================
// Load network from JSON format with JSON type provider

let project = "Json.NET"
let nodeFile = Path.Combine(dataDir, project + ".all.nodes.json")
let linkFile = Path.Combine(dataDir, project + ".all.links.json")

let jsonNodes = Nodes.Load nodeFile

// print node names
for node in jsonNodes.Nodes do printfn "%A" node.Name


let jsonLinks = Links.Load linkFile
 
// ===================================================    
// Create adjacency matrix

open MathNet.Numerics.LinearAlgebra

let nodeCount = jsonNodes.Nodes.Length
let links = 
    seq { for link in jsonLinks.Links -> 
            link.Source, link.Target, 1.0 }
    |> SparseMatrix.ofSeqi nodeCount nodeCount

let nodes = 
    seq { for node in jsonNodes.Nodes -> node.Idx, (node.Name, node.CodeSize)}
    |> dict

// ===================================================
// Network size

let networkSize project =
    let nodeFile = Path.Combine(dataDir, project + ".all.nodes.json")
    let linkFile = Path.Combine(dataDir, project + ".all.links.json")

    let jsonNodes = Nodes.Load nodeFile
    let jsonLinks = Links.Load linkFile

    printfn "%s : Number of nodes is %d" project jsonNodes.Nodes.Length
    printfn "%s : Number of links is %d" project jsonLinks.Links.Length

networkSize "FSharp.Data"
networkSize "Json.NET"

// ***
// ==================================================
// Using R to measure network properties

open RDotNet
open RProvider
open RProvider.``base``
open RProvider.igraph

// Create igraph network
let projectNetwork project = 
    let jsonNodes = Nodes.Load(Path.Combine(dataDir, project + ".all.nodes.json"))
    let jsonLinks = Links.Load(Path.Combine(dataDir, project + ".all.links.json"))
    let nodeCount = jsonNodes.Nodes.Length
    let links = 
        seq { for link in jsonLinks.Links -> link.Source, link.Target, 1.0 }
        |> SparseMatrix.ofSeqi nodeCount nodeCount
        
    links.ToArray()
    |> igraphNetwork    

let jsonNetNetwork = projectNetwork "Json.NET"
let fsharpDataNetwork = projectNetwork "FSharp.Data"

// Network diameter
let d1 = R.diameter(jsonNetNetwork).AsNumeric()
let d2 = R.diameter(fsharpDataNetwork).AsNumeric()

// ***
let size = 3
let m1 = R.graph_motifs(jsonNetNetwork, size).AsNumeric()
let m2 = R.graph_motifs(fsharpDataNetwork, size).AsNumeric()

let motifProfile ms =
    let ms' = Seq.zip [|0..m1.Length-1|] ms
    let norm = 
        ms' |> Seq.sumBy (fun (i,x) -> 
                if x > 0.0 then x else 0.0)          
    ms' |> Seq.map (fun (i,x) -> 
            i, if x > 0.0 then x/norm else 0.0)

let mprofile1 = m1 |> motifProfile
let mprofile2 = m2 |> motifProfile

plotMotifComparison mprofile1 mprofile2

plotMotif 3 5


// ***
// =========================
// Cliques
let maxClique1 = R.largest_cliques(jsonNetNetwork)
let maxClique2 = R.largest_cliques(fsharpDataNetwork)


