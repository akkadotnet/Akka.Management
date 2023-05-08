#I @"tools/FAKE/tools"
#r "FakeLib.dll"

open System
open System.IO
open System.Text

open Fake
open Fake.DotNetCli
open Fake.DocFxHelper

// Information about the project for Nuget and Assembly info files
let configuration = "Release"

// Metadata used when signing packages and DLLs
let signingName = "Akka.Management"
let signingDescription = "Akka.NET Cluster management and bootstrapping libraries"
let signingUrl = "https://getakka.net/"

// Read release notes and version
let solutionFile = FindFirstMatchingFile "*.sln" __SOURCE_DIRECTORY__  // dynamically look up the solution
let buildNumber = environVarOrDefault "BUILD_NUMBER" "0"
let hasTeamCity = (not (buildNumber = "0")) // check if we have the TeamCity environment variable for build # set
let preReleaseVersionSuffix = "beta" + (if (not (buildNumber = "0")) then (buildNumber) else DateTime.UtcNow.Ticks.ToString())

let releaseNotes =
    File.ReadLines (__SOURCE_DIRECTORY__ @@ "RELEASE_NOTES.md")
    |> ReleaseNotesHelper.parseReleaseNotes

let versionFromReleaseNotes =
    match releaseNotes.SemVer.PreRelease with
    | Some r -> r.Origin
    | None -> ""

let versionSuffix = 
    match (getBuildParam "nugetprerelease") with
    | "dev" -> preReleaseVersionSuffix
    | "" -> versionFromReleaseNotes
    | str -> str
    

// Directories
let toolsDir = __SOURCE_DIRECTORY__ @@ "tools"
let output = __SOURCE_DIRECTORY__  @@ "bin"
let outputTests = __SOURCE_DIRECTORY__ @@ "TestResults"
let outputPerfTests = __SOURCE_DIRECTORY__ @@ "PerfResults"
let outputNuGet = output @@ "nuget"

// Configuration values for tests
let testNetVersion = "net6.0"

Target "Clean" (fun _ ->
    ActivateFinalTarget "KillCreatedProcesses"

    CleanDir output
    CleanDir outputTests
    CleanDir outputPerfTests
    CleanDir outputNuGet
    CleanDir "docs/_site"
)

Target "AssemblyInfo" (fun _ ->
    XmlPokeInnerText "./src/Directory.Build.props" "//Project/PropertyGroup/VersionPrefix" releaseNotes.AssemblyVersion    
    XmlPokeInnerText "./src/Directory.Build.props" "//Project/PropertyGroup/PackageReleaseNotes" (releaseNotes.Notes |> String.concat "\n")
)

Target "Build" (fun _ ->          
    let additionalArgs = if versionSuffix.Length > 0 then [sprintf "/p:VersionSuffix=%s" versionSuffix] else []
    DotNetCli.Build
        (fun p -> 
            { p with
                Project = solutionFile
                Configuration = configuration
                AdditionalArgs = additionalArgs }) // "Rebuild"  
)


//--------------------------------------------------------------------------------
// Tests targets 
//--------------------------------------------------------------------------------
module internal ResultHandling =
    let (|OK|Failure|) = function
        | 0 -> OK
        | x -> Failure x

    let buildErrorMessage = function
        | OK -> None
        | Failure errorCode ->
            Some (sprintf "xUnit2 reported an error (Error Code %d)" errorCode)

    let failBuildWithMessage = function
        | DontFailBuild -> traceError
        | _ -> (fun m -> raise(FailedTestsException m))

    let failBuildIfXUnitReportedError errorLevel =
        buildErrorMessage
        >> Option.iter (failBuildWithMessage errorLevel)

Target "RunTests" (fun _ ->
    let projects =
        match (isWindows) with
        | true -> !! "./src/**/*.Tests.*sproj"
        | _ -> !! "./src/**/*.Tests.*sproj" // if you need to filter specs for Linux vs. Windows, do it here

    let runSingleProject project =
        let arguments =
            match (hasTeamCity) with
            | true -> (sprintf "test -c Release --no-build --logger:trx --logger:\"console;verbosity=normal\" --framework %s --results-directory \"%s\" -- -parallel none -teamcity" testNetVersion outputTests)
            | false -> (sprintf "test -c Release --no-build --logger:trx --logger:\"console;verbosity=normal\" --framework %s --results-directory \"%s\" -- -parallel none" testNetVersion outputTests)

        let result = ExecProcess(fun info ->
            info.FileName <- "dotnet"
            info.WorkingDirectory <- (Directory.GetParent project).FullName
            info.Arguments <- arguments) (TimeSpan.FromMinutes 30.0)

        ResultHandling.failBuildIfXUnitReportedError TestRunnerErrorLevel.Error result

    CreateDir outputTests
    projects |> Seq.iter (runSingleProject)
)

Target "NBench" (fun _ ->
    ensureDirectory outputPerfTests
    let nbenchTestAssemblies = !! "./src/**/*Tests.Performance.csproj" 

    nbenchTestAssemblies |> Seq.iter(fun project -> 
        let args = new StringBuilder()
                |> append "run"
                |> append "--no-build"
                |> append "-c"
                |> append configuration
                |> append " -- "
                |> append "--output"
                |> append outputPerfTests
                |> append "--concurrent" 
                |> append "true"
                |> append "--trace"
                |> append "true"
                |> append "--diagnostic"               
                |> toText

        let result = ExecProcess(fun info -> 
            info.FileName <- "dotnet"
            info.WorkingDirectory <- (Directory.GetParent project).FullName
            info.Arguments <- args) (System.TimeSpan.FromMinutes 15.0) (* Reasonably long-running task. *)
        if result <> 0 then failwithf "NBench.Runner failed. %s %s" "dotnet" args
    )
)


//--------------------------------------------------------------------------------
// Code signing targets
//--------------------------------------------------------------------------------
Target "SignPackages" (fun _ ->
    let canSign = hasBuildParam "SignClientSecret" && hasBuildParam "SignClientUser"
    if(canSign) then
        log "Signing information is available."
        
        let assemblies = !! (outputNuGet @@ "*.*upkg")

        let signPath =
            let globalTool = tryFindFileOnPath "SignClient.exe"
            match globalTool with
                | Some t -> t
                | None -> if isWindows then findToolInSubPath "SignClient.exe" "tools/signclient"
                          elif isMacOS then findToolInSubPath "SignClient" "tools/signclient"
                          else findToolInSubPath "SignClient" "tools/signclient"

        let signAssembly assembly =
            let args = StringBuilder()
                    |> append "sign"
                    |> append "--config"
                    |> append (__SOURCE_DIRECTORY__ @@ "appsettings.json") 
                    |> append "-i"
                    |> append assembly
                    |> append "-r"
                    |> append (getBuildParam "SignClientUser")
                    |> append "-s"
                    |> append (getBuildParam "SignClientSecret")
                    |> append "-n"
                    |> append signingName
                    |> append "-d"
                    |> append signingDescription
                    |> append "-u"
                    |> append signingUrl
                    |> toText

            let result = ExecProcess(fun info -> 
                info.FileName <- signPath
                info.WorkingDirectory <- __SOURCE_DIRECTORY__
                info.Arguments <- args) (System.TimeSpan.FromMinutes 5.0) (* Reasonably long-running task. *)
            if result <> 0 then failwithf "SignClient failed.%s" args

        assemblies |> Seq.iter (signAssembly)
    else
        log "SignClientSecret not available. Skipping signing"
)

//--------------------------------------------------------------------------------
// Nuget targets 
//--------------------------------------------------------------------------------

let overrideVersionSuffix (project:string) =
    match project with
    | _ -> versionSuffix // add additional matches to publish different versions for different projects in solution
Target "CreateNuget" (fun _ ->    
    let projects = !! "src/**/*.csproj" 
                   -- "src/**/*Tests.csproj" // Don't publish unit tests
                   -- "src/**/*Tests*.csproj"
                   -- "src/**/examples/**/*.csproj"

    let runSingleProject project =
        DotNetCli.Pack
            (fun p -> 
                { p with
                    Project = project
                    Configuration = configuration
                    AdditionalArgs = ["--include-symbols --no-build --skip-duplicate"]
                    VersionSuffix = overrideVersionSuffix project
                    OutputPath = outputNuGet })

    projects |> Seq.iter (runSingleProject)
)

Target "PublishNuget" (fun _ ->
    let projects = !! "./bin/nuget/*.nupkg" 
    let apiKey = getBuildParamOrDefault "nugetkey" ""
    let source = getBuildParamOrDefault "nugetpublishurl" ""
    let symbolSource = source
    let shouldPublishSymbolsPackages = not (symbolSource = "")

    if (not (source = "") && not (apiKey = "") && shouldPublishSymbolsPackages) then
        let runSingleProject project =
            DotNetCli.RunCommand
                (fun p -> 
                    { p with 
                        TimeOut = TimeSpan.FromMinutes 10. })
                (sprintf "nuget push %s --api-key %s --source %s" project apiKey source)

        projects |> Seq.iter (runSingleProject)
)

//--------------------------------------------------------------------------------
// Docker images
//--------------------------------------------------------------------------------  

let GetDockerProjects =
    let dockerFiles = !! "src/**/Dockerfile" // folders with Dockerfiles in it
       
    let projects = dockerFiles 
                    |> Seq.map (fun dFile -> Path.GetDirectoryName(dFile)) 
                    |> Seq.map (fun folder -> !! (folder + "/*.csproj"))
                    |> Seq.concat

    projects

Target "PublishCode" (fun _ ->    
    ActivateFinalTarget "KillCreatedProcesses"
   
    let projects = GetDockerProjects
        
    let runSingleProject project =
        DotNetCli.Publish
            (fun p -> 
                { p with
                    Project = project
                    Configuration = configuration
                    })

    projects|> Seq.iter (runSingleProject)
)

let mapDockerImageName (projectName:string) =
    match projectName with
    | str -> Some(str.ToLowerInvariant())

Target "BuildDockerImages" (fun _ ->
    let projects = GetDockerProjects

    let remoteRegistryUrl = getBuildParamOrDefault "remoteRegistry" ""

    let composedGetFileNameWithoutExtension (p:string) =
        System.IO.Path.GetFileNameWithoutExtension p

    let buildDockerImage imageName projectPath =
        
        let args = 
            if(hasBuildParam "remoteRegistry") then
                StringBuilder()
                    |> append "build"
                    |> append "-t"
                    |> append (imageName + ":" + releaseNotes.AssemblyVersion) 
                    |> append "-t"
                    |> append (imageName + ":latest") 
                    |> append "-t"
                    |> append (remoteRegistryUrl + "/" + imageName + ":" + releaseNotes.AssemblyVersion) 
                    |> append "-t"
                    |> append (remoteRegistryUrl + "/" + imageName + ":latest") 
                    |> append "."
                    |> toText
            else
                StringBuilder()
                    |> append "build"
                    |> append "-t"
                    |> append (imageName + ":" + releaseNotes.AssemblyVersion) 
                    |> append "-t"
                    |> append (imageName + ":latest") 
                    |> append "."
                    |> toText

        let composedGetDirName (p:string) =
            System.IO.Path.GetDirectoryName p


        ExecProcess(fun info -> 
                info.FileName <- "docker"
                info.WorkingDirectory <- composedGetDirName projectPath
                info.Arguments <- args) (System.TimeSpan.FromMinutes 5.0) (* Reasonably long-running task. *)

    let runSingleProject project =
        let projectName = composedGetFileNameWithoutExtension project
        let imageName = mapDockerImageName projectName
        let result = match imageName with
                        | None -> 0
                        | Some(name) -> buildDockerImage name project
        if result <> 0 then failwithf "docker build failed. %s" project

    projects |> Seq.iter (runSingleProject)
)

//--------------------------------------------------------------------------------
// Documentation 
//--------------------------------------------------------------------------------  
Target "DocFx" (fun _ ->
    DotNetCli.Restore (fun p -> { p with Project = solutionFile })
    DotNetCli.Build (fun p -> { p with Project = solutionFile; Configuration = configuration })

    let docsPath = "./docs"

    DocFx (fun p -> 
                { p with 
                    Timeout = TimeSpan.FromMinutes 30.0; 
                    WorkingDirectory  = docsPath; 
                    DocFxJson = docsPath @@ "docfx.json" })
)

//--------------------------------------------------------------------------------
// Cleanup
//--------------------------------------------------------------------------------

FinalTarget "KillCreatedProcesses" (fun _ ->
    log "Shutting down dotnet build-server"
    let result = ExecProcess(fun info -> 
            info.FileName <- "dotnet"
            info.WorkingDirectory <- __SOURCE_DIRECTORY__
            info.Arguments <- "build-server shutdown") (System.TimeSpan.FromMinutes 2.0)
    if result <> 0 then failwithf "dotnet build-server shutdown failed"
)

//--------------------------------------------------------------------------------
// Help 
//--------------------------------------------------------------------------------

Target "Help" <| fun _ ->
    List.iter printfn [
      "usage:"
      "./build.ps1 [target]"
      ""
      " Targets for building:"
      " * Build         Builds the project"
      " * Nuget         Create and optionally publish nugets packages"
      " * SignPackages  Signs all NuGet packages, provided that the following arguments are passed into the script: SignClientSecret={secret} and SignClientUser={username}"
      " * RunTests      Runs tests"
      " * All           Builds, run tests, creates and optionally publish nuget packages"
      " * DocFx         Creates a DocFx-based website for this solution"
      ""
      " Other Targets"
      " * Help       Display this help" 
      ""]

//--------------------------------------------------------------------------------
//  Target dependencies
//--------------------------------------------------------------------------------

Target "BuildRelease" DoNothing
Target "All" DoNothing
Target "Docker" DoNothing
Target "Nuget" DoNothing

// build dependencies
"Clean" ==> "AssemblyInfo" ==> "Build" ==> "BuildRelease"

// tests dependencies
"Build" ==> "RunTests"
"Build" ==> "NBench"

// nuget dependencies
"Clean" ==> "Build" ==> "CreateNuget"
"CreateNuget" ==> "SignPackages" ==> "PublishNuget" ==> "Nuget"

// docs
"Clean" ==> "BuildRelease" ==> "Docfx"

// Docker
"BuildRelease" ==> "PublishCode" ==> "BuildDockerImages" ==> "Docker"

// all
"BuildRelease" ==> "All"
"RunTests" ==> "All"
"NBench" ==> "All"
"Nuget" ==> "All"

RunTargetOrDefault "Help"
