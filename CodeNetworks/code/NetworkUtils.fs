module NetworkUtils

(*
Helper functions for network analysis
*)

open System
open System.IO
open FSharp.Data
open MathNet.Numerics
open MathNet.Numerics.LinearAlgebra
open MathNet.Numerics.LinearAlgebra.Double
open Projects

open RProvider
open RProvider.``base``
open RProvider.igraph

// It might be necessary to change locations of sample files
type Nodes = JsonProvider<"{\"nodes\":[{\"name\":\"Thing.Runtime.IStuff\",\"idx\": 1,\"codeSize\":157},{\"name\": \"Antlr.Runtime.ICharStream\",\"idx\": 5,\"codeSize\":10}]}">
type Links = JsonProvider<"{\"links\":[{\"source\": 0,\"target\":1},{\"source\":18,\"target\":2}]}">

/// Reads project network from JSON format
let loadNetworkFromJSON dataDir project = 
    let jsonNodes = Nodes.Load(dataDir + project + ".all.nodes.json")
    let jsonLinks = Links.Load(dataDir + project + ".all.links.json")
    
    // Create adjacency matrix
    let nodeCount = jsonNodes.Nodes.Length
    let links = 
        seq { for link in jsonLinks.Links -> link.Source, link.Target, 1.0 }
        |> SparseMatrix.ofSeqi nodeCount nodeCount
    let nodes = 
        seq { for node in jsonNodes.Nodes -> node.Idx, (node.Name, node.CodeSize)}
        |> dict

    // return dictionary of nodes and adjacency matrix
    nodes, links

/// Compute adjacency matrix and save it in csv format
let saveAdjacencyMatrix dataDir project filename = 
    let nodes, links = loadNetworkFromJSON dataDir project

    let csvLinks = 
        [| yield "," +
                ([| for i in 0..nodes.Count-1 -> fst nodes.[i] |] |> String.concat ",")
           for i in 0..nodes.Count-1 do
            yield [| 
                yield string (fst nodes.[i])
                for j in 0..nodes.Count-1 do
                  yield string links.[i,j]
                |]
            |> String.concat "," |]

    File.WriteAllLines(filename, csvLinks)

/// Load adjacency matrix from a csv file
let loadAdjacencyMatrix filename =  
    let lines = File.ReadAllLines filename
    let nodes = (lines.[0].Split [|','|]).[1..]
    let links = 
        lines.[1..]
        |> Seq.map (fun str -> 
            (str.Split [|','|]).[1..]
            |> Seq.map (fun s -> Double.Parse s))
        |> SparseMatrix.ofRowSeq
    links

/// Get number of nodes and number of links in a network from JSON
let networkSize project = 
    let jsonNodes = Nodes.Load(dataDir + "\\" + project + ".all.nodes.json")
    let jsonLinks = Links.Load(dataDir + "\\" + project + ".all.links.json")
    let nodeCount:int = jsonNodes.Nodes.Length
    let linkCount:int = jsonLinks.Links.Length
    nodeCount, linkCount

/// Create igraph network to pass into R functions
let igraphNetwork (adjacencyMatrix : float [,]) = 
    let m = R.as_matrix(adjacencyMatrix)
    let graph = 
        namedParams [
            "adjmatrix", box m; 
            "mode", box "directed"; 
            "weighted", box "NULL"]
        |> R.graph_adjacency
    graph

// Get igraph network for a specific project
let projectNetwork project = 
    let jsonNodes = Nodes.Load(dataDir + project + ".all.nodes.json")
    let jsonLinks = Links.Load(dataDir + project + ".all.links.json")   
    let nodeCount = jsonNodes.Nodes.Length
    let links = 
        seq { for link in jsonLinks.Links -> link.Source, link.Target, 1.0 }
        |> SparseMatrix.ofSeqi nodeCount nodeCount

    Array2D.init nodeCount nodeCount (fun i j -> links.[i,j])
    |> igraphNetwork    

/// Compute diameter of a network
/// takes igraph network as input
let diameter (network:RDotNet.SymbolicExpression) : float =
    R.diameter(network).GetValue()    

/// Compute number of isolated nodes in a network
let isolatedNodes (adjacencyMatrix: Matrix<float>) = 
    // number of rows (columns)
    let n = adjacencyMatrix.RowCount   
    seq { for node in 0..n-1 ->
            if (adjacencyMatrix.Row(node) |> Seq.sum) = 0.0
                && (adjacencyMatrix.Column(node) |> Seq.sum) = 0.0 
            then 1.0 else 0.0 }
    |> Seq.sum

/// Compute number of isolated nodes for a specific project
let isolatedNodeCount project = 
    let adjacencyMatrix = 
        loadAdjacencyMatrix (dataDir + "adjacency//" + project + ".adjacency.csv")            
    isolatedNodes adjacencyMatrix

/// Get total size of code in a project (number of CIL instructions)
let getTotalCodeSize project = 
    let jsonNodes = Nodes.Load(dataDir + project + ".all.nodes.json")
    let nodeSizes = 
        seq { for node in jsonNodes.Nodes -> node.CodeSize }
        |> Seq.sum
    nodeSizes

/// Find and count graph motifs of a specified size
let graphMotifs (size: int) (igraphMatrix : RDotNet.SymbolicExpression) =
    if size <> 3 && size <> 4 then 
        failwith "Only motifs of size 3 and 4 are supported"
    let (motifs: float []) = R.graph_motifs(igraphMatrix, size).GetValue()
    Array.zip [| 0..motifs.Length-1 |] motifs

/// Compute relative motif frequencies for all projects
let motifs k projectNetworks = 
    [| for network in projectNetworks ->
        let projectMotifs = graphMotifs k network
        let totalMotifCount = 
            Array.filter (fun x -> not (Double.IsNaN(snd x))) projectMotifs 
            |> Array.sumBy snd
        if totalMotifCount > 0.0 then
            projectMotifs
            |> Array.map (fun (m, count) -> m, count / totalMotifCount)
        else
            projectMotifs
        |]

/// Count occurences of each motif across projects
let summarizeProfiles (profiles: (int*float)[][]) = 
    [| for i in 0..profiles.[0].Length-1 ->
       [| for j in 0..profiles.Length-1 ->
            profiles.[j].[i] |] |]

/// Plot motif using its isomorphism class 
let plotMotif size isoclass = 
    let isoGraph = R.graph_isocreate(size=size, number=isoclass)
    namedParams [ 
        "x", box isoGraph;
        "edge.arrow.width", box 1;
        "edge.arrow.size", box 1.5;
        "vertex.size", box 50]
    |> R.plot_igraph
    |> ignore

/// Save motif profiles with counts
let exportMotifCounts (projectNames:string[]) projectNetworks k filename = 
    let motifCounts = 
        [| for network in projectNetworks -> 
                graphMotifs k network
                |> Array.filter (fun x -> not (Double.IsNaN(snd x))) |]
    let header = "," + ([ 1..motifCounts.[0].Length ] |> List.map string |> String.concat ",")
    let output = 
        Array.map2 (fun name motifs ->
            let mStrings = Array.map (fun x -> snd x |> string) motifs
            name + "," + String.concat "," mStrings) 
            projectNames motifCounts
 
    File.WriteAllLines(filename, Array.append [|header|] output)

/// Identify motifs that are present only in one type of project (C# or F#)
/// Returns tuples (motif isomorphism class, number of projects where it occurs)
let motifOccurences csMotifs fsMotifs = 
    let countMotifOccurences ms = 
        Array.concat ms
        |> Array.filter (fun (m,f) -> not (Double.IsNaN f))
        |> Seq.groupBy fst
        |> Seq.map (fun (motif, s) -> motif, Seq.filter (fun x -> snd x > 0.0) s |> Seq.length)
        |> Seq.toArray
    
    let fsCounts = countMotifOccurences fsMotifs
    let csCounts = countMotifOccurences csMotifs

    let onlyCsMotifs = 
        Array.zip fsCounts csCounts 
        |> Array.filter (fun ((m1, c_fs),(m2, c_cs)) -> c_fs = 0 && c_cs > 0 )
        |> Array.map snd

    let onlyFsMotifs =
        Array.zip fsCounts csCounts 
        |> Array.filter (fun ((m1, c_fs),(m2, c_cs)) -> c_cs = 0 && c_fs > 0 )
        |> Array.map fst

    onlyCsMotifs, onlyFsMotifs
