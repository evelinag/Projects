(*** hide ***)
#I ".."
#load "packages/FsLab.0.0.14-beta/FsLab.fsx"
open System
(**
Analysing code dependency networks 
=====================================

This [FsLab Journal](http://visualstudiogallery.msdn.microsoft.com/45373b36-2a4c-4b6a-b427-93c7a8effddb)
shows how to analyse code dependency networks from a compiled
.NET assembly.  

Requirements:

- [Mono.Cecil](http://www.mono-project.com/Cecil) - a library for analysis
of compiled .NET assemblies
- [R](http://www.r-project.org/) - statistical programming language that I 
used for motif analysis and for some plotting.

You need to have R installed on your machine, especially to run the motif analysis.
Required NuGet packages including Cecil will download 
when you build this project in Visual Studio.

Extracting dependency network
------------------------------------
As an example, we will work with `FSharp.Data.dll` which is already a part
of this FsLab Journal Project. 
*)

let projectAssembly = __SOURCE_DIRECTORY__ + @"\packages\FSharp.Data.2.0.8\lib\net40\FSharp.Data.dll"

(**
First step is to extract dependecny network from the assembly. Nodes in this 
network are formed by top-level classes or modules. Links represent 
dependencies. 

For this, we will
use some functions defined in `dependency-network.fs`. Code there uses some of 
the code from Scott Wlaschin's blog post [Cycles and modularity in the wild](http://fsharpforfunandprofit.com/posts/cycles-and-modularity-in-the-wild/).
The following function reads dependencies from the assembly and saves the
extracted network in two JSON files, one containing nodes (modules and classes), 
second one containing links (dependencies).
*)

(*[omit:references...]*)

// Set current directory so that Mono.Cecil is able to find FSharp.Core.dll
System.IO.Directory.SetCurrentDirectory(__SOURCE_DIRECTORY__ + "/packages/FSharp.Core/")

#r @"packages\Mono.Cecil.0.9.5.4\lib\net40\Mono.Cecil.dll"
#r @"packages\Mono.Cecil.0.9.5.4\lib\net40\Mono.Cecil.Rocks.dll"
#r @"packages\QuickGraph.3.6.61119.7\lib\net4\QuickGraph.dll"
#load "type-dependency-graph.fs"
(*[/omit]*)

#load "dependency-network.fs"

let outputDir = __SOURCE_DIRECTORY__ + "\\data\\"
let project = "FSharp.Data"

DependencyNetwork.generateNetwork project projectAssembly outputDir

(**
There should now be two files in the `data` folder: 
`FSharp.Data.all.nodes.json` and `FSharp.Data.all.links.json`.

We can examine size of the network using JSON type provider
from `FSharp.Data`.
*)

(*** define-output:networkSize ***)
open FSharp.Data

// JSON type provider - provide samples 
type Nodes = JsonProvider<"../../data/FSharp.Data.all.nodes.json">
type Links = JsonProvider<"../../data/FSharp.Data.all.links.json">

let networkSize project = 
    let jsonNodes = Nodes.Load(outputDir + project + ".all.nodes.json")
    let jsonLinks = Links.Load(outputDir + project + ".all.links.json")
    let nodeCount = jsonNodes.Nodes.Length
    let linkCount = jsonLinks.Links.Length
    nodeCount, linkCount

let nodeCount, linkCount = networkSize project
printfn "Number of nodes: %d, number of links: %d" nodeCount linkCount
(*** include-output:networkSize ***)

(**
This gives us basic size of the network. For further analysis, we will
work with the adjacency matrix representation. Adjacency matrix
is a binary matrix that has value $1$ in row $i$ and column $j$ if there is
 $ i \rightarrow j$ link in the original network. If there is no link, 
 the matrix has $0$ in that position. 
*)

open MathNet.Numerics.LinearAlgebra

let getAdjacencyMatrix project = 
    let jsonNodes = Nodes.Load(outputDir + project + ".all.nodes.json")
    let jsonLinks = Links.Load(outputDir + project + ".all.links.json")
    
    // Create adjacency matrix
    let nodeCount = jsonNodes.Nodes.Length
    seq { for link in jsonLinks.Links -> 
            link.Source, link.Target, 1.0 }
    |> SparseMatrix.ofSeqi nodeCount nodeCount

let adjacencyMatrix = getAdjacencyMatrix project
(**
The resulting adjacency matrix is very sparse:
*)

(*** include-value:adjacencyMatrix ***)   

(**
Computing basic network statistics
-------------------------------------
Using adjacency matrix, we can compute several basic network 
characteristics. For example, how many nodes do not participate
in any dependency? These nodes represent 
standalone modules within the project. In the adjacency matrix, such 
nodes have only zeros in their corresponding rows and columns because
they are not connected to any other node.
*)

(*** define-output:isolated ***)
let isolatedNodes (adjacencyMatrix: Matrix<float>) = 
    // number of rows (columns)
    let n = adjacencyMatrix.RowCount   
    seq { for node in 0..n-1 ->
            if (adjacencyMatrix.Row(node) |> Seq.sum) = 0.0
                && (adjacencyMatrix.Column(node) |> Seq.sum) = 0.0 
            then 1.0 else 0.0 }
    |> Seq.sum

let isolatedCount = isolatedNodes adjacencyMatrix
printfn "Network has %.0f isolated nodes." isolatedCount
(*** include-output:isolated ***)

(**
File with nodes `FSharp.Data.all.nodes.json` contains also information
on the size of code in each node. Code size is equal to the number 
of CIL instructions in all functions (methods) within a module (class).

We can extract the total size of the 
project using JSON type provider.
*)

(*** define-output:codeSize ***)
let getTotalCodeSize project = 
    let jsonNodes = Nodes.Load(outputDir + project + ".all.nodes.json")
    seq { for node in jsonNodes.Nodes -> node.CodeSize }
    |> Seq.sum

let codeSize = getTotalCodeSize project
printfn "Total code size of the project is %d instructions." codeSize
(*** include-output:codeSize ***)

(**
Diameter using R
-------------------------
To compute some more advanced graph statistics, we will turn to R 
with [RProvider](http://bluemountaincapital.github.io/FSharpRProvider/). 
To compute network diameter, we will use the `igraph` package from R.
For this step, you need to have an installation of R on your machine.
To be able to use package `igraph`, you will have to open an R session
and run the following command:
    
    [lang=R]
    install.packages("igraph")

Then you need to restart Visual Studio so that RProvider can detect
installed packages. 

To be able to use the R function for diameters, we need to transform
the adjacency matrix into a format that we can pass into R.
*)

(*** define-output:diameter ***)
open RProvider
open RProvider.``base``
open RProvider.igraph

// transform adjacency matrix into igraph adjacency matrix
let igraphNetwork (adjacencyMatrix: Matrix<float>) =
    Array2D.init adjacencyMatrix.RowCount adjacencyMatrix.ColumnCount
        (fun i j -> adjacencyMatrix.[i,j])
    |> R.as_matrix
    |> R.graph_adjacency

let adjacencyMatrixR = igraphNetwork adjacencyMatrix

// compute diameter of the network
let diameter:float = R.diameter(adjacencyMatrixR).GetValue()
printfn "Diameter of the network is %.0f nodes." diameter    
(*** include-output:diameter ***)

(**
Graph motifs
--------------------------------------
Motifs are small subgraphs with defined structure that occur in graphs.
We will again use R package `igraph` to search for motifs in the project.
The R function `graph_motifs` can efficiently count motifs on 3 or
4 nodes. As an input we only need to supply the R adjacency matrix.
*)

(*** define-output:motif3 ***)
let graphMotifs size =
    if size <> 3 && size <> 4 then 
        failwith "Only motifs of size 3 and 4 are supported."
    let (motifs: float []) = R.graph_motifs(adjacencyMatrixR, size).GetValue()
    Array.zip [| 0..motifs.Length-1 |] motifs

let motifs3 = graphMotifs 3
printfn "%A" motifs3
(*** include-output:motif3 ***)

(**
The function `R.graph_motifs` returns counts of each possible motif on 
3 nodes in the network, ordered by their isomorphism class. 
The F# wrapper function `graphMotifs` returns an array of tupes where the first element
is the isomorphism class of the motif and the second element is the motif count.

Arrangements on 3 nodes that are not connected
are not considered as motifs and the function returns `nan` for those. There are
16 possible subgraphs on 3 nodes but only 13 of them are valid motifs. 

What motif is the most common in `FSharp.Data`?
*)
(*** define-output:mostCommon3 ***)
let mostCommonMotif motifCounts = 
    motifCounts
    |> Array.filter (fun x -> not (Double.IsNaN(snd x)))
    |> Array.maxBy snd

let isoclass, count = mostCommonMotif motifs3
printfn "The most common motif on 3 nodes is motif number %d which occurs %.0f times." isoclass count
(*** include-output:mostCommon3 ***)

(**
The isomprhism class itself does not tell us much about the motif. We can find the 
corresponding graph by plotting it:
*)

(*** define-output:plotMotif3 ***)
let plotMotif size isoclass =
    let isoGraph = R.graph_isocreate(size=size, number=isoclass)
    namedParams [ 
        "x", box isoGraph;
        "edge.arrow.width", box 1;
        "edge.arrow.size", box 1.5;
        "vertex.size", box 50]
    |> R.plot_igraph

plotMotif 3 isoclass
(*** include-output:plotMotif3 ***)

(**
The most common motif in the project is a simple chain of three nodes. 
We can plot the whole motif profile of the project to get an overview of frequencies
of all motifs.
*)

(*** define-output:motifProfile ***)
open FSharp.Charting

Chart.Bar(motifs3, Title="Motif profile", YTitle="Counts", XTitle="Motif")
|> Chart.WithXAxis(Min=0.0,Max=15.0)
(*** include-it:motifProfile ***)

(**
Similarly we can find which motif on 4 nodes is the most common in the project.
*)

(*** define-output:mostCommon4 ***)

let motifs4 = graphMotifs 4
let isoclass4, count4 = mostCommonMotif motifs4
printfn "The most common motif on 4 nodes is motif number %d which occurs %.0f times." isoclass4 count4

(*** include-output:mostCommon4 ***)

(**
Again, we plot the motif to find out what graph the isomoprhism class corresponds to.
*)

(*** define-output:plotMotif4 ***)
plotMotif 4 isoclass4
(*** include-output:plotMotif4 ***)
