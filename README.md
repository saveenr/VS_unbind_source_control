# VS Unbind Source Control

VS-Unbind-Source-Control is a simple way to strip out all the source control bindings from your Visual Studio solution and project files.

## Scenario

Have you ever sent your Visual Studio solution to someone and when they open it up Visual Studio complains about source control bindings? This is a pretty common scenario when your project is under source control. 

## Usage

The tool works on a directory and **MODIFIES AND DELETES FILES IN-PLACE**. 

So to prevent the loss of files, first, copy your solution to a new directory. 

Then, run this command: 

```
VSUnbindSourceControl.exe d:\myfolder  
```

## Disclaimer

Use the tool at your own risk. By downloading or using the tool you agree to its license

## Notes

* I've only tried this against CSharp projects
* I've only tried this using Visual Studio 2010 and Visual Studio 2012 

## Origins

The project builds on code by Jake Opines. The significant changes in this project include:

* Simplified I/O
* More specific cleanup of project file XML
* Uses System.Xml.Linq instead of System.Xml
* Indented formatting of output project files
* Built using Visual Studio 2012
* Compiles .NET Framework 4.0
