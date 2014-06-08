module DependencyNetwork

open System
open System.IO
open FSharp.Data

open TypeDependencyGraph
open TypeDependencyGraph.AssemblyTypes

module JsonNetwork =

    /// Get sizes of nodes (modules) from an assemby
    /// Count instructions in non-core methods only
    let nodesCodeSize excludeCore assemblyFileName =
        Mono.Cecil.AssemblyDefinition.ReadAssembly(fileName=assemblyFileName).MainModule.Types
        |> Seq.filter (isNotCoreType excludeCore)
        |> Seq.collect withAllChildTypes
        |> Seq.map (fun td -> 
            let methodSize = 
                td.Methods |> Seq.map (fun md -> 
                    if md.HasBody then md.Body.CodeSize else 0) 
                |> Seq.sum
            td.FullName, methodSize)
        // clean up module names
        |> Seq.filter (fun (md, sz) -> not (md.Contains "<" && md.Contains ">")) 
        |> Seq.map (fun (md, sz) -> 
            let mdname = md.[0.. (if md.Contains "/" then md.IndexOf("/")-1 else md.Length-1)]
            mdname, sz)
        |> Seq.groupBy fst
        |> Seq.map (fun (md, ssz) -> md, ssz |> Seq.map snd |> Seq.sum)

    /// Extract name of a top-level type
    let getName (dep:TltName) = match dep with TltName(str) -> str

    /// Create a node record in JSON from a dependency set
    let assemblyNode (dep:DependencySet) (nameToIdx:Collections.Generic.IDictionary<string,int>) (moduleSizes: (string*int) seq) =
        let name = getName dep.dependent
        let idx = decimal nameToIdx.[name]
        let size = 
            moduleSizes 
            |> Seq.filter (fun (n, s) -> n = name)
            |> Seq.exactlyOne
            |> snd
            |> decimal
        JsonValue.Record [| 
            ("name", JsonValue.String name); 
            ("idx", JsonValue.Number idx); 
            ("codeSize", JsonValue.Number size) |]

    /// Save nodes in the network in JSON format
    let saveAssemblyNodes dependencies nameToIdx (moduleSizes: (string*int) seq) outputFile = 
        let nodes = dependencies |> Seq.map (fun n -> assemblyNode n nameToIdx moduleSizes) |> Seq.toArray
        let jsonNodes = 
            [| ("nodes", (JsonValue.Array nodes)) |]
            |> JsonValue.Record
        File.WriteAllText(outputFile, jsonNodes.ToString())

    /// Create JSON records for links in a dependency set
    let assemblyLinks (dep:DependencySet) (nameToIdx:Collections.Generic.IDictionary<string,int>) = 
        let name = getName dep.dependent
        let srcIdx = nameToIdx.[name]
        let tgtIdxs = 
            dep.dependencies 
            |> Seq.map getName
            |> Seq.map (fun d -> nameToIdx.[d])
        let links = 
            tgtIdxs
            |> Seq.map (fun dependencyTgt ->
                // dependency target is the source of the arrow,
                // dependency source is the target of the arrow
                JsonValue.Record 
                    [| ("source", JsonValue.Number (decimal dependencyTgt)); 
                       ("target", JsonValue.Number (decimal srcIdx)) |])
        links

    /// Save links in the network in JSON format
    let saveAssemblyLinks dependencies nameToIdx outputFile = 
        let links = dependencies |> Seq.collect (fun d -> assemblyLinks d nameToIdx) |> Seq.toArray
        let jsonLinks = 
            [| ("links", (JsonValue.Array links)) |] |> JsonValue.Record
        File.WriteAllText(outputFile, jsonLinks.ToString())

    /// Convert dependencies into JSON format and save them
    let convertDependencies (deps:DependencySet seq) (moduleSizes: (string*int) seq) projectName outputDir = 
        // add zero-based index to modules
        let depIdxs, depArr = deps |> Seq.toArray |> Array.mapi (fun i x -> (i,x)) |> Array.unzip
        let depDict = 
            Array.zip depArr depIdxs
            |> Array.map (fun ((d:DependencySet), i) -> 
                let name = match d.dependent with TltName(str) -> str
                (name, i))
            |> dict 

        if not (Directory.Exists outputDir) then Directory.CreateDirectory outputDir |> ignore
        let outputNodes = outputDir + "\\" + projectName + ".nodes.json"
        let outputLinks = outputDir + "\\" + projectName + ".links.json"

        saveAssemblyNodes depArr depDict moduleSizes outputNodes
        saveAssemblyLinks depArr depDict outputLinks


// ------------------------
// Main function
// ------------------------

/// Extract dependency network from assembly and save it in JSON format
let generateNetwork projectName assemblyName outputDir = 
    let excludeCore = ignoreCoreTypes projectName 

    // analyze
    let topLevelTypes = AssemblyTypes.topLevelTypes excludeCore assemblyName 
    let allDeps = TopLevelTypeDependencies.allDependencies topLevelTypes 

    let moduleSizes = JsonNetwork.nodesCodeSize excludeCore assemblyName 

    // Generate Networks
    JsonNetwork.convertDependencies allDeps moduleSizes (projectName + ".all") outputDir
