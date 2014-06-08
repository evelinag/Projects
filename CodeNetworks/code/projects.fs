module Projects

// ===============================================
// List of analyzed projects
// ===============================================

let dataDir = __SOURCE_DIRECTORY__ + "/networks/"

let csDir = __SOURCE_DIRECTORY__ + "/packages/cs/"
let fsDir = __SOURCE_DIRECTORY__ + "/packages/fs/"

let csProjects = 
    [|("Antlr", @"Antlr\lib\Antlr3.Runtime.dll");
      ("AutoMapper", @"AutoMapper\lib\net40\AutoMapper.dll");
      ("Castle.Core", @"Castle.Core\lib\net40-client\Castle.Core.dll");
      ("elmah.corelibrary", @"elmah.corelibrary\lib\Elmah.dll");
      ("EntityFramework", @"EntityFramework\lib\net40\EntityFramework.dll");
      ("FParsec", @"FParsec\lib\net40-client\FParsecCS.dll");
      ("log4net", @"log4net\lib\net40-client\log4net.dll");
      ("MathNet.Numerics", @"MathNet.Numerics\lib\net40\MathNet.Numerics.dll");
      ("Microsoft.AspNet.SignalR.Core", @"Microsoft.AspNet.SignalR.Core\lib\net45\Microsoft.AspNet.SignalR.Core.dll");
      ("Microsoft.Bcl", @"Microsoft.Bcl\lib\net40\System.Runtime.dll");
      ("Microsoft.Owin", @"Microsoft.Owin\lib\net40\Microsoft.Owin.dll");
      ("Mono.Cecil", @"Mono.Cecil\lib\net40\Mono.Cecil.dll");
      ("Moq", @"Moq\lib\net40\Moq.dll");
      ("Nancy", @"Nancy\lib\net40\Nancy.dll");
      ("Newtonsoft.Json", @"Newtonsoft.Json\lib\net40\Newtonsoft.Json.dll");
      ("Nuget.Core", @"Nuget.Core\lib\net40-Client\NuGet.Core.dll");
      ("NUnit", @"NUnit\lib\nunit.framework.dll");
      ("SpecFlow", @"SpecFlow\lib\net35\TechTalk.SpecFlow.dll");
      ("xunit", @"xunit\lib\net20\xunit.dll");
      ("YamlDotNet", @"YamlDotNet\lib\net35\YamlDotNet.dll");
      |] |> Array.map (fun (name, file) -> name, csDir + file)
    

let fsProjects = 
    [|("canopy", @"canopy\lib\canopy.dll");
      ("Deedle", @"Deedle\lib\net40\Deedle.dll");
      ("Fake", @"Fake\tools\Fake.exe");
      ("Foq", @"Foq\Lib\net40\Foq.dll");
      ("FParsec", @"FParsec\lib\net40-client\FParsec.dll");
      ("FsCheck", @"FsCheck\lib\net40-Client\FsCheck.dll");
      ("FSharp.Compiler.Service", @"FSharp.Compiler.Service\lib\net40\FSharp.Compiler.Service.dll");
      ("FSharp.Core", @"FSharp.Core\lib\FSharp.Core.dll");
      ("FSharp.Data", @"FSharp.Data\lib\net40\FSharp.Data.dll");
      ("FSharp.Data.Twitter", @"FSharp.Data.Toolbox.Twitter\lib\net40\FSharp.Data.Toolbox.Twitter.dll");
      ("FSharpx.Core", @"FSharpx.Core\lib\40\FSharpx.Core.dll");
      ("FsPowerPack.Core.Community", @"FSPowerPack.Core.Community\Lib\Net40\FSharp.PowerPack.dll");
      ("FsSql", @"FsSql\lib\FsSql.dll");
      ("FsUnit", @"FsUnit\Lib\Net40\FsUnit.NUnit.dll");
      ("FsYaml", @"FsYaml\lib\net40\FsYaml.dll");
      ("Storm", @"Storm\Storm.exe");
      ("TickSpec",@"TickSpec\Lib\net40\TickSpec.dll");
      ("WebSharper",@"WebSharper\tools\net40\IntelliFactory.WebSharper.dll");
      ("WebSharper.Core",@"WebSharper\tools\net40\IntelliFactory.WebSharper.Core.dll");
      ("WebSharper.Html",@"WebSharper\tools\net40\IntelliFactory.WebSharper.Html.dll")
      |] |> Array.map (fun (name, file) -> name, fsDir + file)

let allProjects = Array.append csProjects fsProjects

let csProjectNames = csProjects |> Array.map fst
let fsProjectNames = fsProjects |> Array.map fst
let allProjectNames = allProjects |> Array.map fst