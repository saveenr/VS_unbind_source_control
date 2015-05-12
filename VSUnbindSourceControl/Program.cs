using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

// 2013-08-03 - Fixed Regex.IsMatch parameters
// 2013-08-03 - No longer try to parse VDPROJ files

namespace VSUnbindSourceControl
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                System.Console.WriteLine("ERROR: No folder specified");
                System.Console.WriteLine("SYNTAX: VSUnbindSourceControl <folder>");
                System.Console.WriteLine("Stopping.");
                return;
            }

            string folder = args[0].Trim();

            if (folder.Length < 1)
            {
                System.Console.WriteLine("ERROR: empty folder name");
                System.Console.WriteLine("Stopping.");
                return;
            }

            if (!System.IO.Directory.Exists(folder))
            {
                System.Console.WriteLine("ERROR: Folder does not exist");
                System.Console.WriteLine("Stopping.");
                return;
            }

            Console.WriteLine("Starting");

            var scc_files_to_delete = new List<string>();
            var proj_files_to_modify = new List<string>();
            var sln_files_to_modify = new List<string>();
            
            var files = new List<string>( System.IO.Directory.GetFiles( folder, "*.*", SearchOption.AllDirectories ) );

            foreach (var filename in files)
            {
                string normalized_filename = filename.ToLower();
                if (normalized_filename.Contains(".") && normalized_filename.EndsWith("proj") && !normalized_filename.EndsWith("vdproj"))
                {
                    proj_files_to_modify.Add(filename);
                }
                else if (normalized_filename.EndsWith(".sln"))
                {
                    sln_files_to_modify.Add(filename);
                }
                else if (normalized_filename.EndsWith(".vssscc") || normalized_filename.EndsWith(".vspscc"))
                {
                    scc_files_to_delete.Add(filename);
                }
                else
                {
                    // do nothing
                }
            }

            if ((proj_files_to_modify.Count + sln_files_to_modify.Count + scc_files_to_delete.Count < 1))
            {
                System.Console.WriteLine("No files to modify or delete. Exiting.");
                return;
            }

            foreach (var file in sln_files_to_modify)
            {
                ModifySolutionFile(file);
            }

            foreach (var file in proj_files_to_modify)
            {
                ModifyProjectFile(file);
            }

            foreach (var file in scc_files_to_delete)
            {
                DeleteFile(file);
            }
            System.Console.WriteLine("Done.");
        }

        public static void ModifySolutionFile(string filename)
        {
            if (!filename.ToLower().EndsWith(".sln"))
            {
                throw new System.ArgumentException("Internal Error: ModifySolutionFile called with a file that is not a solution");
            }

            Console.WriteLine("Modifying Solution: {0}", filename);
            
            // Remove the read-only flag
            var original_attr = System.IO.File.GetAttributes(filename);
            System.IO.File.SetAttributes(filename, System.IO.FileAttributes.Normal);

            var output_lines = new List<string>();

            bool in_sourcecontrol_section = false;
            var lines = System.IO.File.ReadAllLines(filename);
            foreach (string line in lines)
            {
                var line_trimmed = line.Trim();

                // lines can contain separators which interferes with the regex
                // escape them to prevent regex from having problems
                line_trimmed = Uri.EscapeDataString(line_trimmed);


                if (line_trimmed.StartsWith("GlobalSection(SourceCodeControl)") 
                    || line_trimmed.StartsWith("GlobalSection(TeamFoundationVersionControl)")
                    || System.Text.RegularExpressions.Regex.IsMatch(line_trimmed, "GlobalSection(.*Version.*Control", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    // this means we are starting a Source Control Section
                    // do not copy the line to output
                    in_sourcecontrol_section = true;
                }
                else if (in_sourcecontrol_section && line_trimmed.StartsWith("EndGlobalSection"))
                {
                    // This means we were Source Control section and now see the ending marker
                    // do not copy the line containing the ending marker 
                    in_sourcecontrol_section = false;
                }
                else if (line_trimmed.StartsWith("Scc"))
                {
                    // These lines should be ignored completely no matter where they are seen
                }
                else
                {
                    // No handle every other line
                    // Basically as long as we are not in a source control section
                    // then that line can be copied to output

                    if (!in_sourcecontrol_section)
                    {
                        output_lines.Add(line);
                    }
                }
            }

            // Write the file back out
            System.IO.File.WriteAllLines(filename,output_lines);

            // Restore the original file attributes
            System.IO.File.SetAttributes(filename, original_attr);

        }

        public static void ModifyProjectFile(string filename)
        {
            if (!filename.ToLower().EndsWith("proj"))
            {
                throw new System.ArgumentException("Internal Error: ModifyProjectFile called with a file that is not a project");
            }

            Console.WriteLine("Modifying Project : {0}", filename);
            
            // Load the Project file
            var doc = System.Xml.Linq.XDocument.Load(filename);

            // Modify the Source Control Elements
            RemoveSCCElementsAttributes(doc.Root);
            
            // Remove the read-only flag
            var original_attr = System.IO.File.GetAttributes(filename);
            System.IO.File.SetAttributes(filename, System.IO.FileAttributes.Normal);

            // Write out the XML
            using (var writer = new System.Xml.XmlTextWriter(filename, Encoding.UTF8))
            {
                writer.Formatting = System.Xml.Formatting.Indented;
                doc.Save(writer);
                writer.Close();
            }

            // Restore the original file attributes
            System.IO.File.SetAttributes(filename, original_attr);
        }

        private static void RemoveSCCElementsAttributes(System.Xml.Linq.XElement el)
        {
            el.Elements().Where( x => x.Name.LocalName.StartsWith( "Scc" ) ).Remove();
            el.Attributes().Where( x => x.Name.LocalName.StartsWith( "Scc" ) ).Remove();

            foreach( var child in el.Elements() )
            {
                RemoveSCCElementsAttributes(child);
            }
        }

        public static void DeleteFile(string filename)
        {
            System.IO.File.SetAttributes(filename, System.IO.FileAttributes.Normal);
            System.IO.File.Delete(filename);
        }
    }
}
