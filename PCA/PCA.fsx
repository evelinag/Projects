//=============================================
// Part 1: Books from Project Gutenberg

open System
open System.IO

// Strip book text off project Gutenberg text

type BookPart = | Start | End

let isBookPart part (line:string) =
    let keyword = 
        match part with
        | Start -> "START"
        | End -> "END"
    (line.Contains (keyword + " OF THIS PROJECT GUTENBERG EBOOK"))
    || (line.Contains (keyword + " OF THE PROJECT GUTENBERG EBOOK"))

let bookStarted line = isBookPart Start line
let bookFinished line = isBookPart End line

// Find book title in the beginning of the file
let getBookTitle (lines: string[]) = 
    lines.[0..50]
    |> Array.pick (fun str -> 
        if str.Contains "Title:" then 
            Some(str.[6..].Trim() 
            |> String.map (fun letter -> 
                if letter = ',' then ';' else letter))
        else None)            

let cleanText (lines: string[]) =
    lines
    |> Seq.skipWhile (fun str -> not (bookStarted str))
    |> Seq.takeWhile (fun str -> not (bookFinished str))
    |> String.concat " "
    |> Seq.map (fun c -> 
        if Char.IsLetter c then Char.ToLowerInvariant c
        else ' ')

let loadBook filename =
    let fullContents = File.ReadAllLines filename
    let title = getBookTitle fullContents
    let text = cleanText fullContents
    title, text

// =======================================
// Create a vector of all letter pairs
let alphabet = " abcdefghijklmnopqrstuvwxyz"
let letterPairs =
    [| for a in alphabet do 
        for b in alphabet do
            if a <> ' ' || b <> ' ' then yield [|a; b|] |]

// Extract bigram counts from a specific book
let bookBigrams filename =
    let title, text = loadBook filename

    let rawBigrams =
        text
        |> Seq.windowed 2
        |> Seq.countBy id
        |> dict

    let bigramCountVector =
        letterPairs 
        |> Array.map (fun pair -> 
            if rawBigrams.ContainsKey pair then rawBigrams.[pair]
            else 0)

    title, bigramCountVector


// =========================================
#time "on"
let filename = @"C:\Temp\books\Dickens\pg46.txt"
let title, data = bookBigrams filename

// most common bigrams
Array.zip letterPairs data
|> Array.sortBy (fun x -> - snd x)
|> Seq.take 3

// =========================================
// Compute for all books
// and save the computed bigram counts into a file

#r "packages/FSharp.Collections.ParallelSeq/lib/net40/FSharp.Collections.ParallelSeq.dll"
open FSharp.Collections.ParallelSeq

let booksDir = @"C:\Temp\books"
let authorDirs =
    Directory.GetDirectories(booksDir)

let authorBigrams =
    authorDirs
    |> Array.map (fun d -> 
        printfn "%s" d
        Directory.GetFiles(d)
        |> PSeq.map (fun file ->
            printfn "* %s" file
            bookBigrams file)
        |> PSeq.toArray
        )

// Real: 00:06:41.957, CPU: 00:14:24.312, GC gen0: 5307, gen1: 67, gen2: 8

// Save as csv file
let header = 
    "Author,Book," 
    + (letterPairs 
        |> Array.map (fun cs -> 
            cs |> Array.map string |> String.concat "") 
        |> String.concat ",")

let lines = 
    Array.zip authorDirs authorBigrams
    |> Array.map (fun (d, data) ->
        let author = Path.GetFileName(d)
        data 
        |> Array.map (fun (b, xs) ->
            author + "," + b
            + "," + (xs |> Array.map string |> String.concat ","))
        )
    |> Array.concat

let file = @"C:\Temp\books\bigramValues.csv"
File.WriteAllLines(file, Array.append [|header|] lines)

// ===================================
// ===================================

// read csv file, extract data
#r "packages/MathNet.Numerics/lib/net40/MathNet.Numerics.dll"
#r "packages/MathNet.Numerics.FSharp/lib/net40/MathNet.Numerics.FSharp.dll"

#load "packages/FSharp.Charting.0.90.9/FSharp.Charting.fsx"
open MathNet.Numerics
open MathNet.Numerics.LinearAlgebra
open MathNet.Numerics.LinearAlgebra.Double
open System
open System.IO
open FSharp.Charting

// Load the full dataset
let dataFile = @"C:\Temp\books\bigramValues.csv"

let authors, titles, fullData = 
    (dataFile |> File.ReadAllLines).[1..]
    |> Array.map (fun line -> 
        printfn "%s" line
        let fullLine = line.Split [|','|]
        fullLine.[0], fullLine.[1], 
        fullLine.[2..] |> Array.map (fun x -> Double.Parse x))
    |> Array.unzip3

// Filter out letter combinations that are least used
// total number: 676
fullData.[1] |> Seq.sortBy (fun x -> -x) |> Chart.Line

// =====================================
// Preprocessing

fullData

// Sort bigrams by frequency
let sortedBigrams = 
    [| for idx in 0..letterPairs.Length - 1 ->
        let sum = fullData |> Array.sumBy (fun xs -> xs.[idx])
        idx, sum |]
    |> Array.sortBy (fun (idx, sum) -> -sum)
    |> Array.map fst

// How many bigrams to keep
let bigramCount = 400
let mostUsedBigrams = sortedBigrams.[0..bigramCount-1] |> set

let chooseBigrams bigramValues =
    bigramValues 
    |> Array.mapi (fun i x -> 
        if mostUsedBigrams.Contains i then Some(x) else None) 
    |> Array.choose id    

// Normalize matrix to relative frequencies
let normalize (xs: float[]) =
    let total = Array.sum xs
    xs |> Array.map (fun x -> x/total) 

let dataMatrix = 
    fullData
    |> Array.map (fun xs -> chooseBigrams xs)    
    |> Array.map (fun xs -> normalize xs)
    |> DenseMatrix.ofColumnArrays

// =========================================
// PCA

// Mean vector of the whole dataset
let center = 
    let n = dataMatrix.ColumnCount |> float
    dataMatrix.RowSums() / n

// Normalize data to zero mean
let centeredMatrix = 
    let sumMatrix = Array.init dataMatrix.ColumnCount (fun _ -> center)
                   |> DenseMatrix.OfColumnVectors
    dataMatrix - sumMatrix

centeredMatrix.RowSums()

// Compute eigenvalue decomposition of the covariance matrix
let covarianceMatrix = 
    centeredMatrix * centeredMatrix.Transpose()
let evd = covarianceMatrix.Evd()

// Choose eigenvectors corresponding to largest eigenvalues

let eigenvectors d = 
    let m = evd.EigenVectors
    m.[0..m.RowCount-1, m.ColumnCount-d..m.ColumnCount-1]

let projection d = (eigenvectors d).Transpose() * centeredMatrix
let projected2D = 
    let projectedData = projection 2
    [| for idx in 0..projectedData.ColumnCount-1 -> projectedData.[0,idx], projectedData.[1,idx] |]

// Visualisation - group by authors
let byAuthor = 
    projected2D
    |> Array.zip authors
    |> Seq.groupBy fst
    |> Array.ofSeq
    |> Array.map (fun (name, xs ) -> name, Seq.map snd xs)

byAuthor
|> Array.map (fun (name, bookVectors) -> Chart.Point(bookVectors, MarkerSize=12,Name=name))
|> Chart.Combine
|> Chart.WithLegend(InsideArea=false)
|> Chart.WithXAxis(Min=(-0.012), Max=0.015)

titles |> Array.iteri (fun i t -> printfn "%A %s" i t)
let christmasCarolIdx = 15

// plot eigenvalues 
let eigenvalues = 
    evd.EigenValues
    |> Seq.map (fun c -> c.Real)
    |> Array.ofSeq
    |> Array.rev

eigenvalues
|> Chart.Line
        
// K nearest neighbours
let projectedData = projection 20
let christmasCarol = projectedData.Column(christmasCarolIdx)

let kNearest k = 
    [| for idx, v in projectedData.EnumerateColumnsIndexed() do
        if idx <> christmasCarolIdx then
            let distance = (v - christmasCarol)*(v - christmasCarol)
            yield titles.[idx], distance |]
    |> Seq.sortBy snd
    |> Seq.take k
    |> Array.ofSeq

kNearest 3

