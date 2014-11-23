#load @"packages\FsLab.0.0.19\FsLab.fsx"

open System
open System.IO

open MathNet.Numerics
open MathNet.Numerics.LinearAlgebra
open MathNet.Numerics.LinearAlgebra.Double
open RProvider
open RProvider.``base``
open RProvider.graphics
open FSharp.Data

open System
open System.IO
open System.Collections.Generic


// Loading JSON data with type providers
// ===================================================

let twitterHandle = "fsharporg"
let nodeFile = @"C:\Temp\Twitter\networks\" + twitterHandle + "Nodes_22-11-2014.json"
let linkFile = @"C:\Temp\Twitter\networks\" + twitterHandle + "Links_22-11-2014.json"

type Users = JsonProvider<"{\"nodes\": [{\"name\": \"screenname\",\"id\": 123245623993}]}">
let userNames = Users.Load nodeFile
userNames.Nodes.Length

//
userNames.Nodes.[0].Name


type Connections = JsonProvider<"{\"links\":[{\"source\": 3,\"target\": 765}]}">
let userLinks = Connections.Load linkFile

// Helper functions for Twitter IDs
// ===================================================
// Read ID numbers and screen names of accounts in the ego network
// Helper functions to facilitate easy translation between the two

let idToName = dict [ for node in userNames.Nodes -> node.Id, node.Name ] 
let nameToId = dict [ for node in userNames.Nodes -> node.Name, node.Id ]


// Introduce indices (starting from 0) to number nodes in the network
let idxToId, idToIdx =
    let idxList, idList =
        userNames.Nodes
        |> Array.mapi (fun idx node -> (idx,node.Id), (node.Id, idx))
        |> Array.unzip
    dict idxList, dict idList

// Translate index to Twitter Id and Name
let idxToIdName idx =
    let id = idxToId.[idx]
    id, idToName.[id]

// Find idex for a specific screen name
let nameToIdx screenName =
    let id = nameToId.[screenName]
    idToIdx.[id]

// Usage
idxToIdName (nameToIdx "dsyme")

// Sparse adjacency matrix
// ===================================================

// Read links in the network into an adjacency matrix
let nodeCount = userNames.Nodes.Length
let links = 
    seq { for link in userLinks.Links -> link.Source, link.Target, 1.0 }
    |> SparseMatrix.ofSeqi nodeCount nodeCount

// Out-degree and in-degree
// ===================================================

let outdegree (linkMatrix:float Matrix) =
    [| for outlinks in linkMatrix.EnumerateRows() -> 
        outlinks.Sum() |]   

let indegree (linkMatrix: float Matrix) =
    [| for inlinks in linkMatrix.EnumerateColumns() -> inlinks.Sum() |]

let degree linkMatrix = Array.map2 (+) (outdegree linkMatrix) (indegree linkMatrix)

// In-degree and out-degree with matrix multiplication
// ====================================================================

let outdegreeFaster (linkMatrix: float Matrix) =
    linkMatrix * DenseVector.Create(linkMatrix.RowCount, 1.0)

let indegreeFaster (linkMatrix: float Matrix) =
    DenseVector.Create(linkMatrix.ColumnCount, 1.0) * linkMatrix

#time
let indegrees = indegree links
let outdegrees = outdegree links
outdegreeFaster links

let degrees = degree links

// Degree distribution of nodes
// ==============================================
// Visualize degree distribution with R provider
R.plot(indegrees)
R.hist(indegrees,50)

// Log-log plot of degree distribution
let degreeDist ds = ds |> Seq.countBy id
    
let degreeValues, degreeCounts = 
    degreeDist indegrees 
    |> List.ofSeq |> List.unzip

namedParams [
    "x", box degreeValues;
    "y", box degreeCounts;
    "log", box "xy";
    "pch", box 16;
    "col", box "royalblue";
    "xlab", box "Log degree";
    "ylab", box "Log frequency"]
    |> R.plot

// Top users from a ranking
// ===================================================
// Find top ranking users
let topUsers count (ranking:float seq) = 
    ranking
    |> Seq.mapi (fun i x -> (i,x))
    |> Seq.sortBy (fun (i,x) -> - x)
    |> Seq.choose (fun (i,x) -> 
        let id, name = idxToIdName i
        if name <> "*****" then Some(id, name, x)
        else None)
    |> Seq.take count

// Get a list of people that have most followers
indegrees
|> topUsers 10
|> Seq.iteri (fun i (id, name, value) ->
    printfn "%d. %s has indegree %.0f" (i+1) name value)    

// Transition matrix
// ===================================================

// Transition matrix - gives transition probabilities
// T[i,j] = probability of transition from i to j
//        = 1/(outdegree[i])
let transitionBasic = 
    seq { for i, j, _ in links.EnumerateNonZeroIndexed() -> 
            i, j, 1.0/outdegrees.[i] }
    |> SparseMatrix.ofSeqi nodeCount nodeCount

// Correct for dangling nodes (nodes with no outcoming links)
let transitionMatrix =
    seq { for r, row in transitionBasic.EnumerateRowsIndexed() ->
            // if there are no outgoing links, create links to all
            // other nodes in the network with equal probability
            if row.Sum() = 0.0 then
                SparseVector.init nodeCount (fun i -> 
                        1.0/(float nodeCount))
            else row }
    |> SparseMatrix.ofRowSeq

// Mapper and reducer functions
// ===================================================
// MapReduce in steps
// 1) Map 
let mapper (transitionMatrix:Matrix<float>) (pageRank:float []) = 
    seq { for (src, tgt, prob) in transitionMatrix.EnumerateNonZeroIndexed() do
            yield (tgt, pageRank.[src]*prob) 
          for node in 0..transitionMatrix.RowCount-1 do
            yield (node, 0.0) }
    
// 2) Reduce
// random jump factor
let d = 0.85 

let reducer nodeCount (mapperOut: (int*float) seq) = 
    mapperOut
    |> Seq.groupBy fst  // collect values for each key (node)
    |> Seq.sortBy fst
    |> Seq.map (fun (node, inRanks) ->
        let inRankSum = inRanks |> Seq.sumBy snd
        d * inRankSum + (1.0-d)/(float nodeCount))
    |> Seq.toArray

// PageRank algorithm
// ===========================================================
// Create a vector to hold the page rank values
// and initialize with equal values (1/number of nodes)
// (equal probability of being in any node in the network)
let startPageRank = Array.create nodeCount (1.0/(float nodeCount))
let minDifference = 1e-10  
let maxIter = 100

let rec pageRank iters (transitionMatrix:Matrix<float>) (pageRankVals : float []) = 
    if iters = 0 then pageRankVals
    else
        let newPageRanks = 
            pageRankVals
            |> mapper transitionMatrix
            |> reducer nodeCount

        let difference = 
            Array.map2 (fun r1 r2 -> abs (r1 - r2)) pageRankVals newPageRanks
            |> Array.sum
        if difference < minDifference then
            printfn "Converged in iteration %i" (maxIter - iters)
            newPageRanks
        else pageRank (iters-1) transitionMatrix newPageRanks
            
// Run on the full transition matrix
let pageRankValues = pageRank maxIter transitionMatrix startPageRank

pageRankValues
|> topUsers 100
|> Seq.iteri (fun i (id, name, value) ->
    printfn "%d. @%s has PageRank %f" (i+1) name value)    

// Plot PageRank distribution
R.hist(pageRankValues,100)

// JSON file for nodes with PageRank information
// ==========================================================

let jsonUsersPR userIdx userPR = 
    let id, name = idxToIdName userIdx
    JsonValue.Record [|
        "name", JsonValue.String name
        "id", JsonValue.Number (decimal id)
        "r", JsonValue.Float userPR |]

let jsonNodes = 
    let jsonPR = Array.mapi (fun idx rank -> jsonUsersPR idx rank) pageRankValues
    JsonValue.Record [| "nodes", (JsonValue.Array jsonPR) |]      

let filename = @"C:\Temp\Twitter\visualisation\"
File.WriteAllText(filename + twitterHandle + "pageRankNodes.json", jsonNodes.ToString())
