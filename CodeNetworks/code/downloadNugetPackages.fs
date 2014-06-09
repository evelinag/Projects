module FsSnip.NuGet

(*
Script for downloading assemblies from NuGet

Downloaded from F# Snippets 
http://fssnip.net/cd
*)

open System
open System.IO
open System.Net
open Ionic.Zip

let getPackageUrl = sprintf "http://packages.nuget.org/api/v1/package/%s/"

let getPackage name downloadDir = async {
    let url = getPackageUrl name
    let req = WebRequest.Create(url)
    use! resp = req.AsyncGetResponse()
    use incoming = resp.GetResponseStream()
    
    // The response stream doesn't support Seek, and ZipFile needs it,
    // so read into a MemoryStream
    use ms = new MemoryStream()
    let buffer = Array.zeroCreate<byte> 0x1000
    let more = ref true
    while !more do
        let! read = incoming.AsyncRead(buffer, 0, buffer.Length)
        if read = 0 then
            more := false
        else
            ms.Write(buffer, 0, read)

    ms.Seek(0L, SeekOrigin.Begin) |> ignore
    use zip = ZipFile.Read(ms)
    let dir, name = 
        match downloadDir with
        | None ->
            let tempName = Path.GetTempFileName()
            File.Delete tempName // GetTempFileName creates a file, we want a folder
            let dirName = tempName + "/" + name + "/"
            Directory.CreateDirectory dirName, dirName
        | Some(d) -> 
            let dirName = d + "/" + name + "/"
            if Directory.Exists dirName then Directory.Delete(dirName, true)
            Directory.CreateDirectory dirName, dirName
    zip.ExtractAll name
    return dir
}

let ensureDelete (dir:DirectoryInfo) =
    { new IDisposable with
        member x.Dispose() =
            dir.Delete(true) }

let findAssemblies (dir:DirectoryInfo) =
    let rec filterAssemblies (dir:DirectoryInfo) = seq {
        for item in dir.EnumerateFileSystemInfos() do
            match item with
            | :? DirectoryInfo as d ->
                match d.Name.ToLowerInvariant() with
                | "lib" | "net20" | "net35" | "net40" 
                | "net40-full" | "net40-client" | "net45" ->
                    yield! filterAssemblies d
                | _ -> ()
            | :? FileInfo as f ->
                if f.Extension = ".dll" then
                    yield f
            | _ -> ()
    }

    let getPlatform (f:FileInfo) =
        let idx = f.DirectoryName.LastIndexOf('\\')
        f.DirectoryName.[idx + 1 ..].ToLowerInvariant()

    let allAssemblies =
        filterAssemblies dir
        |> Seq.cache

    let platforms =
        allAssemblies
        |> Seq.map getPlatform
        |> Set.ofSeq

    let newestPlatforms =
        platforms
        |> Set.filter 
            (function
            | "lib" | "net45" -> true
            | "net20" -> 
                [ "net35"; "net40"; "net40-full"; "net40-client"; "net45" ] 
                |> Set.ofList 
                |> Set.intersect platforms 
                |> Set.isEmpty
            | "net35" ->
                [ "net40"; "net40-full"; "net40-client"; "net45" ] 
                |> Set.ofList 
                |> Set.intersect platforms 
                |> Set.isEmpty
            | "net40-client" ->
                [ "net40"; "net40-full"; "net45" ] 
                |> Set.ofList 
                |> Set.intersect platforms 
                |> Set.isEmpty
            | "net40" ->
                [ "net40-full"; "net45" ] 
                |> Set.ofList 
                |> Set.intersect platforms 
                |> Set.isEmpty
            | "net40-full" ->
                platforms.Contains "net45" |> not
            | _ -> false)

    allAssemblies
    |> Seq.filter (getPlatform >> newestPlatforms.Contains)

let downloadPackage dir name =
    printfn "Downloading %s..." name
    if name = "Storm" then
        printfn "Storm has to be downloaded manually from CodePlex."
    else if name.Contains "WebSharper." then 
        // WebSharper has multiple dlls, it needs to be downloaded just once
        printfn "Skipping %s" name
    else
        let packageDir =
            getPackage name (Some dir)
            |> Async.RunSynchronously

        findAssemblies packageDir
        |> Seq.iter (fun f -> printfn "%s" f.FullName)

