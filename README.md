# vs-unbind-source-control


VS-Unbind-Source-Control is a simple way to strip out all the source control bindings from your Visual Studio solution and project files.

# SCENARIO

Have you ever sent your Visual Studio solution to someone and when they open it up Visual Studio complains about source control bindings? This is a pretty common scenario when your project is under source control. 

# USAGE

First, Copy your solution to a new directory. Why copy? Because the tool modify files in place.

Then, Run this command: 

VSUnbindSourceControl.exe d:\myfolder  

# DISCLAIMER

Use the tool at your own risk. By downloading or using the tool you agree to its license

# NOTES

I've only tried this against CSharp projects
I've only tried this using Visual Studio 2010 and Visual Studio 2012 

# ORIGINS

The code is originally based on this project by Jake Opines. The significant changes here include

Much simplified I/O
More specific cleanup of Project file XML
Use of System.Xml.Linq instead of System.Xml
Indented formatting of output project files
Compiles using VS2012 and using .NET Framework 4.0
 
