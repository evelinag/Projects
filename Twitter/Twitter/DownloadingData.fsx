
#r @"./packages/FSharp.Data.2.0.9/lib/net40/FSharp.Data.dll"
#r @"./packages/FSharp.Data.Toolbox.Twitter.0.2.1/lib/net40/FSharp.Data.Toolbox.Twitter.dll"

open System
open System.IO
open System.Threading

open FSharp.Data
open FSharp.Data.Toolbox.Twitter


let user = "evelgab"

// Connecting to Twitter
// ==================================================

// Application credentials
let key = "CoqmPIJ553Tuwe2eQgfKA"
let secret = "dhaad3d7DreAFBPawEIbzesS1F232FnDsuWWwRTUg"

// Full authentication
let connector = Twitter.Authenticate(key, secret)
// A window appers for Twitter sign-in
// After authentication, a PIN should appear
// Use the PIN as an argument for the Connect function
let twitter = connector.Connect("2525211")

let t = twitter.Users.Lookup(["pigworker"])
        |> Array.ofSeq

t.[0].FriendsCount

// Looking at interactions
// ==================================================

let rec searchTweets hashtag lastTweetId remainingRequests tweets =
    printfn "%d" remainingRequests
    if remainingRequests = 0 then tweets else
    let ts = 
        match lastTweetId with
        | None -> 
            twitter.Search.Tweets(hashtag, count=100)
        | Some(id) -> 
            twitter.Search.Tweets(hashtag, maxId = id, count=100)
    if ts.Statuses.Length = 0 then tweets
    else 
        let lastId = ts.Statuses.[ts.Statuses.Length-1].Id |> Some
        searchTweets hashtag lastId (remainingRequests-1) 
            (Array.append tweets ts.Statuses)
        

let hashtag = "#fsharp"
let tweets = searchTweets hashtag None 100 [||]

printfn "Number of downloaded tweets: %d" 
    tweets.Length

// interactions
let interactions = 
    [| for tweet in tweets -> 
        tweet.RetweetCount, tweet.FavoriteCount |]
    |> Array.filter (fun (r,f) -> r>0 || f>0)

// interaction rate
let interactionRate = 
    float interactions.Length / float tweets.Length

// Correlations
open FSharp.Charting
Chart.Point(interactions, XTitle="Retweeted", YTitle="Favourited")

open MathNet.Numerics.Statistics
Array.map (fun (x,y) -> float x, float y) interactions
|> Array.unzip
|> Correlation.Pearson



// Downloading the data
// ==================================================

let friends = twitter.Connections.FriendsIds(screenName="@" + user) 
let followers = twitter.Connections.FollowerIds(screenName="@" + user)

//
friends.Ids |> Seq.length
followers.Ids |> Seq.length

// Create a set of accounts 
let idsOfInterest = Seq.append friends.Ids followers.Ids |> set
printfn "Size of ego network: %d" idsOfInterest.Count

// Twitter screen names from user ID numbers 
// ==================================================

// Limits: 180 requests per 15 minutes (with full authentication)
// i.e. 1 request per 5 seconds

// Download user information
let twitterNodes = 
    [| for id in idsOfInterest do
        Thread.Sleep(5000)
        let nodeInfo = 
            try 
                twitter.Users.Lookup([id])
            with _ -> 
                // recently cancelled accounts etc.
                printfn "Unable to access ID %d" id
                [||]      
        yield! nodeInfo |]
    |> Array.map (fun node -> node.Id, node.ScreenName)
    
// Twitter connections between users
// ==================================================
// Beware, downloading Twitter connections is a long process due to 
// access rate limits. I recommend running this on a server with a stable 
// internet connection. 

let isInNetwork id = idsOfInterest.Contains id

// Get connections from Twitter
let twitterConnections (ids:int64 seq) =
    [| for srcId in ids do
        Thread.Sleep(60000)     // wait for one minute
        printfn "Downloading connections for: %d" srcId
        let connections = 
            try 
                // Get IDs of friends and keep
                // only nodes that are connected to the central node
                twitter.Connections.FriendsIds(srcId).Ids
                |> Array.filter isInNetwork 
            with _ -> 
                // accounts with hidden list of friends and followers etc
                printfn "Unable to access ID %i" srcId
                [||]      
        // return source and target
        yield! connections |> Seq.map (fun tgtId -> srcId, tgtId)|]


// Export network’s nodes into JSON
// =====================================================

// Set directory for saving results
let currentDirectory = @"C:\Users\Public\Documents\Twitter\"
if not (Directory.Exists currentDirectory) then Directory.CreateDirectory currentDirectory |> ignore
Directory.SetCurrentDirectory currentDirectory


let jsonNode (userInfo: int64*string) = 
    let id, name = userInfo
    JsonValue.Record [| 
            "name", JsonValue.String name
            "id", JsonValue.Number (decimal id) |] 

let jsonNodes = 
    let nodes = twitterNodes |> Array.map jsonNode
    [|"nodes", (JsonValue.Array nodes) |]
    |> JsonValue.Record
File.WriteAllText(user + "Nodes.json", jsonNodes.ToString())

// Export network’s links into JSON
// ======================================================

// Helper functions to translate between Twitter IDs to zero-based indices
let idToIdx =
    idsOfInterest 
    |> Seq.mapi (fun idx id -> (id, idx))
    |> dict

// Save links in JSON format
let jsonConnections (srcId, tgtId) = 
    let src = idToIdx.[srcId]
    let tgt = idToIdx.[tgtId]
    JsonValue.Record [|
            "source", JsonValue.Number (decimal src)
            "target", JsonValue.Number (decimal tgt) |] 

let jsonLinks = 
    let linkArr =
        twitterConnections idsOfInterest
        |> Array.map jsonConnections
        |> JsonValue.Array
    JsonValue.Record [|"links", linkArr|]
File.WriteAllText(user + "Links.json", jsonLinks.ToString())





