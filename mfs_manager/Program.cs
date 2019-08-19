using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace mfs_manager
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("64DD MFS Manager - by LuigiBlood");
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: mfs_manager <RAM file> <options>");
                Console.WriteLine("<options> can be the following:");
                Console.WriteLine("  -d                       : List all directories with their IDs");
                Console.WriteLine("  -f                       : List all files");
                Console.WriteLine("  -e                       : Extract all files");
                Console.WriteLine("  -e <File Path>           : Extract specified file (must start with \"/\")");
                Console.WriteLine("  -i <File> <Directory ID> : Insert <File> into <Directory ID>");
            }
            else if (args.Length > 1)
            {
                MFSDisk mfsDisk = new MFSDisk(args[0]);
                if (mfsDisk.Format == MFS.DiskFormat.Invalid)
                {
                    Console.WriteLine("Error loading RAM file");
                }

                if (args[1].Equals("-d"))
                {
                    foreach (MFSEntry entry in mfsDisk.RAMVolume.Entries)
                    {
                        if (entry.GetType() == typeof(MFSDirectory))
                        {
                            Console.WriteLine(((MFSDirectory)entry).DirectoryID + ": " + MFSRAMUtil.GetFullEntryPath(mfsDisk, entry));
                        }
                    }
                }
                else if (args[1].Equals("-f"))
                {
                    foreach (MFSEntry entry in mfsDisk.RAMVolume.Entries)
                    {
                        if (entry.GetType() == typeof(MFSFile))
                        {
                            Console.WriteLine(MFSRAMUtil.GetFullEntryPath(mfsDisk, entry));
                        }
                    }
                }
                else if (args[1].Equals("-e"))
                {
                    if (args.Length > 2)
                    {
                        Console.WriteLine("Extract " + args[2]);
                        byte[] data = MFSRAMUtil.ReadFile(mfsDisk, args[2]);
                        if (data != null)
                        {
                            FileStream fileExt = new FileStream(".\\extract\\" + Path.GetFileName(args[2]), FileMode.Create);
                            fileExt.Write(data, 0, data.Length);
                            fileExt.Close();
                            Console.WriteLine("Done");
                        }
                        else
                        {
                            Console.WriteLine("Error");
                        }
                    }
                    else
                    {
                        foreach (MFSEntry entry in mfsDisk.RAMVolume.Entries)
                        {
                            if (entry.GetType() == typeof(MFSFile))
                            {
                                Console.WriteLine("Extract " + MFSRAMUtil.GetFullEntryPath(mfsDisk, entry));
                                byte[] data = MFSRAMUtil.ReadFile(mfsDisk, (MFSFile)entry);
                                FileStream fileExt = new FileStream(".\\extract\\" + entry.Name + (((MFSFile)entry).Ext != "" ? "." : "") + ((MFSFile)entry).Ext, FileMode.Create);
                                fileExt.Write(data, 0, data.Length);
                                fileExt.Close();
                            }
                        }
                    }
                }
                else if (args[1].Equals("-i") && args.Length > 3)
                {
                    FileStream testAdd = new FileStream(args[2], FileMode.Open);
                    byte[] testArray = new byte[testAdd.Length];
                    testAdd.Read(testArray, 0, (int)testAdd.Length);
                    testAdd.Close();
                    if (!MFSRAMUtil.InsertFile(mfsDisk, testArray, Path.GetFileName(args[2]), ushort.Parse(args[3])))
                        Console.WriteLine("Could not insert file");
                    else
                        Console.WriteLine("File inserted successfully");
                    mfsDisk.Save(args[0] + ".new.ram");
                }
            }
        }
    }
}
