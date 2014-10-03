namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("FunFoundation")>]
[<assembly: AssemblyProductAttribute("FunFoundation")>]
[<assembly: AssemblyDescriptionAttribute("An FSharp client for FoundationDb")>]
[<assembly: AssemblyVersionAttribute("1.0")>]
[<assembly: AssemblyFileVersionAttribute("1.0")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "1.0"
