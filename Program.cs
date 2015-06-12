using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace RecreateFilter
{
    class Program
    {
        private static List<string> FolderList = new List<string>();
        private static List<string> FolderIgnoreList = new List<string>();
        private static List<string> FileList = new List<string>();
        private static List<string> FileIgnoreList = new List<string>();

        static void Main(string[] args)
        {
            Console.WriteLine("Visual Studio 2013 - Filter Refresher for C projects:\n\n");

            var workingDir = Environment.CurrentDirectory;
            var workingDirInfo = new DirectoryInfo(workingDir);

            // collect project files and check current directory makefile
            var projectFiles = workingDirInfo.GetFiles("*.vcxproj");
            var filterFiles = workingDirInfo.GetFiles("*.vcxproj.filters");

            var makeFiles = workingDirInfo.GetFiles("Makefile");
            if (makeFiles.Length == 0 || !makeFiles[0].Exists)
            {
                // otherwise, check parent directory
                workingDir = Directory.GetParent(Environment.CurrentDirectory).ToString();
                workingDirInfo = new DirectoryInfo(workingDir);
                makeFiles = workingDirInfo.GetFiles("Makefile");
            }

            if (projectFiles.Length == 0 || filterFiles.Length == 0)
            {
                Console.WriteLine("Files not found in path: \n- " + Environment.CurrentDirectory + "\n- " + Environment.CurrentDirectory + "\nFilepatters: *.vcproj and *.vcproj.filters");
            }
            else if (projectFiles.Length > 1 || filterFiles.Length > 1)
            {
                Console.WriteLine("To many files in path " + Environment.CurrentDirectory +
                                  ". \nFilepatters: *.vcproj and *.vcproj.filters");
            }
            else
            {
                // look for .gitignore file to collect directories to be ignored
                var ignoreFiles = workingDirInfo.GetFiles(".gitignore");
                if (ignoreFiles.Length > 0 && ignoreFiles[0].Exists)
                {
                    Console.WriteLine("Using git ignore file: " + workingDir + @"\.gitignore");
                    FolderIgnoreList = new List<string>();
                    string line;
                    var ignoreFile = new StreamReader(workingDir + @"\.gitignore");
                    while ((line = ignoreFile.ReadLine()) != null)
                    {
                        if (line.EndsWith("/")) FolderIgnoreList.Add(line.TrimEnd('/'));
                        else FileIgnoreList.Add(line);
                    }
                    ignoreFile.Close();

                    if (FolderIgnoreList.Count > 0)
                    {
                        Console.WriteLine("Ignoring:");
                        foreach (var ignoredDir in FolderIgnoreList)
                            Console.WriteLine("- " + ignoredDir);
                    }
                    else Console.WriteLine("No folders found to ignore");

                }

                // collect files and folders
                FetchFolderList(workingDir);
                FetchFileList(workingDir);

                Console.WriteLine("\nFound " + FileList.Count + " files in " + FolderList.Count + " folders");

                Console.WriteLine("\nProcessed:");

                projectFiles[0].CopyTo(projectFiles[0].FullName + ".bak", true); // make backup
                ProcessFiles(workingDir, projectFiles[0].Name);
                Console.WriteLine(" - " + projectFiles[0].FullName);

                filterFiles[0].CopyTo(filterFiles[0].FullName + ".bak", true); // make backup
                ProcessFilters(workingDir, filterFiles[0].Name);
                Console.WriteLine(" - " + filterFiles[0].FullName);
            }
            Console.WriteLine("\nFinished");
            Console.ReadKey();
        }

        private static void FetchFolderList(string path, int indent = 0)
        {
            try
            {
                foreach (string folder in Directory.GetDirectories(path))
                    if (!FolderIgnoreList.Contains(Path.GetFileName(folder)))
                    {
                        Console.WriteLine("{0}{1}", new string(' ', indent), Path.GetFileName(folder));
                        FolderList.Add(folder);
                        FetchFolderList(folder, indent + 2);
                    }
            }
            catch (UnauthorizedAccessException) { }
        }

        public static void FetchFileList(string root)
        {
            var dirs = new Stack<string>(20);
            dirs.Push(root);

            while (dirs.Count > 0)
            {
                string currentDir = dirs.Pop();
                if (!FolderIgnoreList.Contains(Path.GetFileName(currentDir)))
                // TO DO: make more intelligent currently code can omit directories in the tree that have a name defined in .gitignore, change to filter relative path folder structure
                {
                    string[] subDirs = new string[0];
                    try
                    {
                        subDirs = Directory.GetDirectories(currentDir);
                        string[] files = Directory.GetFiles(currentDir);
                        foreach (string file in files)
                        {
                            try
                            {
                                FileList.Add(file);
                            }
                            catch (FileNotFoundException e)
                            {
                                Console.WriteLine("File no longer exists " + e.Message);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("File or folder inaccesible " + e.Message);
                    }

                    foreach (string str in subDirs)
                        dirs.Push(str);
                }
            }
        }

        private static void ProcessFiles(string workingDir, string projectFile)
        {
            // read .filter file       
            var xmldoc = new XmlDocument();
            xmldoc.Load(Environment.CurrentDirectory + @"\" + projectFile);
            const string nameSpace = "http://schemas.microsoft.com/developer/msbuild/2003";
            var NS = AttachNamespaces(xmldoc, nameSpace);
            var itemGroup = xmldoc.SelectSingleNode("/ns:Project/ns:ItemGroup[not(@Label)]", NS);
            if (itemGroup != null)
            {
                itemGroup.RemoveAll();

                // add files
                foreach (var file in FileList)
                {
                    var itemType = "None";
                    if (Path.GetExtension(file) == ".c" || Path.GetExtension(file) == ".cc") itemType = "ClCompile";
                    else if (Path.GetExtension(file) == ".h") itemType = "ClInclude";

                    XmlNode fileItem = itemGroup.OwnerDocument.CreateNode(XmlNodeType.Element, itemType, nameSpace);
                    var fileAttribute = itemGroup.OwnerDocument.CreateAttribute("Include");
                    fileAttribute.InnerText = file.Replace(workingDir + "\\", (workingDir != Environment.CurrentDirectory ? @"..\" : ""));
                    fileItem.Attributes.Append(fileAttribute);
                    itemGroup.AppendChild(fileItem);
                }
            }

            xmldoc.Save(Environment.CurrentDirectory + @"\" + projectFile);
        }

        private static void ProcessFilters(string workingDir, string filterFile)
        {
            // read .filter file       
            var xmldoc = new XmlDocument();
            xmldoc.Load(Environment.CurrentDirectory + @"\" + filterFile);
            const string nameSpace = "http://schemas.microsoft.com/developer/msbuild/2003";
            var NS = AttachNamespaces(xmldoc, nameSpace);
            var project = xmldoc.SelectSingleNode("//ns:Project", NS);
            if (project != null)
            {
                project.RemoveAll();

                // add folders
                XmlNode folderNode = project.OwnerDocument.CreateNode(XmlNodeType.Element, "ItemGroup", nameSpace);
                foreach (var folder in FolderList)
                {
                    XmlNode folderItem = project.OwnerDocument.CreateNode(XmlNodeType.Element, "Filter", nameSpace);
                    var folderAttribute = project.OwnerDocument.CreateAttribute("Include");
                    folderAttribute.InnerText = folder.Replace(workingDir + "\\", "");
                    folderItem.Attributes.Append(folderAttribute);

                    XmlNode identfier = project.OwnerDocument.CreateNode(XmlNodeType.Element, "UniqueIdentifier", nameSpace);
                    identfier.InnerText = "{" + Guid.NewGuid() + "}";
                    folderItem.AppendChild(identfier);
                    folderNode.AppendChild(folderItem);
                }
                project.AppendChild(folderNode);

                // add files
                XmlNode fileNode = project.OwnerDocument.CreateNode(XmlNodeType.Element, "ItemGroup", nameSpace);
                foreach (var file in FileList)
                {
                    var itemType = "None";
                    if (Path.GetExtension(file) == ".c" || Path.GetExtension(file) == ".cc") itemType = "ClCompile";
                    else if (Path.GetExtension(file) == ".h") itemType = "ClInclude";

                    XmlNode fileItem = project.OwnerDocument.CreateNode(XmlNodeType.Element, itemType, nameSpace);
                    var fileAttribute = project.OwnerDocument.CreateAttribute("Include");
                    fileAttribute.InnerText = file.Replace(workingDir + "\\", (workingDir != Environment.CurrentDirectory ? "..\\" : ""));
                    fileItem.Attributes.Append(fileAttribute);

                    XmlNode identfier = project.OwnerDocument.CreateNode(XmlNodeType.Element, "Filter", nameSpace);
                    identfier.InnerText = Path.GetDirectoryName(file).Replace(workingDir + "\\", "");
                    fileItem.AppendChild(identfier);
                    fileNode.AppendChild(fileItem);
                }
                project.AppendChild(fileNode);
            }

            xmldoc.Save(Environment.CurrentDirectory + @"\" + filterFile);
        }

        private static XmlNamespaceManager AttachNamespaces(XmlDocument xmldoc, string nameSpace)
        {
            var nsMgr = new XmlNamespaceManager(xmldoc.NameTable);
            XmlNode rootnode = xmldoc.DocumentElement;
            var strTest = GetAttribute(ref rootnode, "xmlns");
            nsMgr.AddNamespace("ns", string.IsNullOrEmpty(strTest) ? nameSpace : strTest);

            // Add namespaces from XML root tag
            if (rootnode.Attributes != null)
                foreach (XmlAttribute attr in rootnode.Attributes)
                {
                    string attrname = attr.Name;
                    if (attrname.IndexOf("xmlns", StringComparison.Ordinal) == 0 && !string.IsNullOrEmpty(attrname))
                    {
                        if (attrname.Length == 5) // default Namespace
                        {
                            string ns = "default";
                            nsMgr.AddNamespace(ns, attr.Value);
                        }
                        else if (attrname.Length > 6)
                        {
                            string ns = attrname.Substring(6);
                            nsMgr.AddNamespace(ns, attr.Value);
                        }
                    }
                }

            return nsMgr;
        }

        public static string GetAttribute(ref XmlNode node, string AttributeName, string DefaultValue = "")
        {
            XmlAttribute attribute = default(XmlAttribute);
            if (node != null)
                attribute = node.Attributes[AttributeName];

            if (attribute != null)
                return (string.IsNullOrEmpty(node.Attributes[AttributeName].Value)
                    ? DefaultValue
                    : node.Attributes[AttributeName].Value);
            else return DefaultValue;
        }
    }
}
