using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Xml;

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

            ProcessFile(ModifySolutionFile, sln_files_to_modify);
            ProcessFile(ModifyProjectFile, proj_files_to_modify);
            ProcessFile(DeleteFile, scc_files_to_delete);

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

            Encoding encoding;
            var lines = ReadAllLines(filename, out encoding);

            foreach (string line in lines)
            {
                var line_trimmed = line.Trim();

                // lines can contain separators which interferes with the regex
                // escape them to prevent regex from having problems
                line_trimmed = Uri.EscapeDataString(line_trimmed);


                if (line_trimmed.StartsWith("GlobalSection(SourceCodeControl)") 
                    || line_trimmed.StartsWith("GlobalSection(TeamFoundationVersionControl)")
                    || System.Text.RegularExpressions.Regex.IsMatch(line_trimmed, @"GlobalSection\(.*Version.*Control", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
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
            System.IO.File.WriteAllLines(filename, output_lines, encoding);

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
            XDocument doc = null;
            Encoding encoding = new UTF8Encoding(false);
            using (StreamReader reader = new StreamReader(filename, encoding))
            {
                doc = System.Xml.Linq.XDocument.Load(reader);
                encoding = reader.CurrentEncoding;
            }
                
            // Modify the Source Control Elements
            RemoveSCCElementsAttributes(doc.Root);
            
            // Remove the read-only flag
            var original_attr = System.IO.File.GetAttributes(filename);
            System.IO.File.SetAttributes(filename, System.IO.FileAttributes.Normal);

            //if the original document doesn't include the encoding attribute 
            //in the declaration then do not write it to the outpu file.
            if (String.IsNullOrEmpty(doc.Declaration.Encoding))
                encoding = null;
            
            //else if its not utf (i.e. utf-8, utf-16, utf32) format which use a BOM
            //then use the encoding identified in the XML file.
            else if(!doc.Declaration.Encoding.StartsWith("utf", StringComparison.OrdinalIgnoreCase))
                encoding = Encoding.GetEncoding(doc.Declaration.Encoding);
                
            // Write out the XML
            using (var writer = new System.Xml.XmlTextWriter(filename, encoding))
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

        /// <summary>
        /// Reads all the lines from a test file into an array.
        /// </summary>
        /// <param name="path">The file to open for reading.</param>
        /// <param name="encoding">The file encoding.</param>
        /// <returns>A string array containing all the lines from the file</returns>
        /// <remarks>UTF-8 encoded files optionally include a byte order mark (BOM) at the beginning of the file.
        /// If the mark is detected by the StreamReader class, it will modify it's encoding property so that it
        /// reflects that file was written with a BOM. However, if no BOM is detected the StreamReader will not
        /// modify it encoding property. The determined UTF-8 encoding (UTF-8 with BOM or UTF-8 without BOM) is
        /// returned as an output parameter.
        /// </remarks>
        private static string[] ReadAllLines(string path, out Encoding encoding)
        {
            List<string> lines = new List<string>();

            Encoding encodingNoBom = new UTF8Encoding(false);
            using(StreamReader reader = new StreamReader(path, encodingNoBom))
            {
                while (!reader.EndOfStream)
                {
                    lines.Add(reader.ReadLine());
                }

                encoding = reader.CurrentEncoding;
            }

            return lines.ToArray();
        }

        /// <summary>
        /// Processes a list of files based on the porcessing method.
        /// </summary>
        /// <param name="processMethod">The method for processing the files.</param>
        /// <param name="files">The list of files.</param>
        private static void ProcessFile(Action<string> processMethod, IEnumerable<string> files)
        {
            foreach (var file in files)
            {
                try
                {
                    processMethod(file);
                }
                catch(Exception e)
                {
                    string message = String.Format("Unable to process {0}: {1}", file, e.Message);
                    WriteLine(ConsoleColor.Red, message);
                }
            }
        }

        /// <summary>
        /// Writes a line to console in the specified foreground color.
        /// </summary>
        /// <param name="foregroundColor">The foreground color.</param>
        /// <param name="value">The value that is written to the console.</param>
        private static void WriteLine(ConsoleColor foregroundColor, string value)
        {
            ConsoleColor current = Console.ForegroundColor;
            Console.ForegroundColor = foregroundColor;
            Console.WriteLine(value);
            Console.ForegroundColor = current;
        }
    }
}
