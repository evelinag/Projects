
#load @"packages\FsLab.0.0.19\FsLab.fsx"

open System
open System.IO

open MathNet.Numerics
open MathNet.Numerics.LinearAlgebra
open MathNet.Numerics.LinearAlgebra.Double
open RProvider
open RProvider.``base``
open RProvider.graphics
open RProvider.igraph
open RProvider.Matrix
open RProvider.rARPACK
open RProvider.scatterplot3d
open RDotNet
open FSharp.Data

open System
open System.IO
open System.Collections.Generic

// To run this code and use R type providers
// you need to have R installed with the following packages:
//  - igraph
//  - Matrix 
//  - rARPACK
//  - scatterplot3d (for visualization)


// Loading JSON data with type providers
// ===================================================

let twitterHandle = "fsharporg"
let nodeFile = @"C:\Temp\Twitter\networks\" + twitterHandle + "Nodes_22-11-2014.json"
let linkFile = @"C:\Temp\Twitter\networks\" + twitterHandle + "Links_22-11-2014.json"witterHandle + "Links_22-11-2014.json"|]

type Users = JsonProvider<"{\"nodes\": [{\"name\": \"screenname\",\"id\": 123245623993}]}">
let userNames = Users.Load nodeFile

type Connections = JsonProvider<"{\"links\":[{\"source\": 3,\"target\": 765}]}">
let userLinks = Connections.Load linkFile

// Read links in the network into an adjacency matrix
let nodeCount = userNames.Nodes.Length
let links = 
    seq { for link in userLinks.Links -> link.Source, link.Target, 1.0 }
    |> SparseMatrix.ofSeqi nodeCount nodeCount

// -------------------------------------------
// Create non-backtracking matrix
// -------------------------------------------
let edgeCount = userLinks.Links.Length
let size = 2 * edgeCount

// translate edges into indices
let edgeToIdx = 
    userLinks.Links 
    |> Seq.mapi (fun i link -> (link.Source, link.Target), i)
    |> dict

// Create sparse non-backtracking matrix using R package Matrix
let m =
    let is, js = 
        [| for (source1, target1, _) in links.EnumerateNonZeroIndexed() do
              for (source2, target2, _ ) in links.EnumerateNonZeroIndexed() do
                    if target1 = source2 && source1 <> target2 then
                        let i1 = edgeToIdx.[(source1, target1)]
                        let i2 = edgeToIdx.[(source2, target2)]
                        yield (i1 + 1, i2 + 1) |]
        |> Array.unzip
    namedParams ["i", box is; "j", box js; "x", box 1.0; "dims", box [edgeCount; edgeCount]]
    |> R.sparseMatrix

// Eigenvectors and eigenvalues
let e = R.eigs(A=m, k=10, which="LR", opts="list(maxitr=10000,NCV=500)").AsList()

// Eigenvector corresp. to the largest eigenvalue 
let centrality = e.[1].AsNumericMatrix().[0..edgeCount-1,0] |> Seq.toArray

let getIncomingEdges nodeIdx = 
    seq { for (src,_) in links.Column(nodeIdx).EnumerateNonZeroIndexed() -> 
            edgeToIdx.[(src, nodeIdx)] }

let nodeValues = 
    [| for n in 0..nodeCount-1 -> 
        getIncomingEdges n 
        |> Seq.map (fun i -> centrality.[i]) 
        |> Seq.sum |]

R.hist(nodeValues)

let nameValues = 
    userNames.Nodes |> Array.map (fun n -> n.Name) |> Array.zip nodeValues
    |> Array.sortBy (fun (x,name) -> x)

nameValues.[0..20]
|> Array.iteri (fun i (_,name) -> printfn "%s" name)

// The rest of the eigenvalues correspond to community structure
// (all eigenvalues that are larger or equal to sqrt(largest eigenvalue))
let communityLinks = 
    [|(e.[1].AsNumericMatrix().[0..edgeCount-1,1] |> Seq.toArray)
      (e.[1].AsNumericMatrix().[0..edgeCount-1,2] |> Seq.toArray)
      (e.[1].AsNumericMatrix().[0..edgeCount-1,3] |> Seq.toArray)|]
let communityNodes1, communityNodes2, communityNodes3 = 
    [| for n in 0..nodeCount-1 -> 
        let i1, i2, i3 =
            getIncomingEdges n
            |> Seq.map (fun i -> communityLinks.[0].[i], communityLinks.[1].[i], communityLinks.[2].[i])
            |> Seq.toArray
            |> Array.unzip3 
        Array.sum i1, Array.sum i2, Array.sum i3|]
    |> Array.unzip3

R.plot(communityNodes1, communityNodes2)

R.scatterplot3d(communityNodes1, communityNodes2, communityNodes3)

