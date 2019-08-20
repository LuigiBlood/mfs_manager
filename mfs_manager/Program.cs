using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace mfs_manager
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.Unicode;
            Console.WriteLine("64DD MFS Manager - by LuigiBlood");
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: mfs_manager <RAM file> <options>");
                Console.WriteLine("<options> can be the following:");
                Console.WriteLine("  -d                            : List all directories with their IDs");
                Console.WriteLine("  -f                            : List all files");
                Console.WriteLine("  -e                            : Extract all files");
                Console.WriteLine("  -e <File Path>                : Extract specified file (must start with \"/\")");
                Console.WriteLine("  -i <File> <Directory Path/ID> : Insert <File> into <Directory Path/ID> (Path must start AND end with \"/\")");
                Console.WriteLine("  -r <File Path>                : Delete <File> (Path must start with \"/\")");
                Console.WriteLine("  -m <File Path> <Dir/File Path>: Move <File> to <Directory> AND/OR rename if <File Path> (Path must start with \"/\")");
            }
            else if (args.Length >= 2)
            {
                MFSDisk mfsDisk = new MFSDisk(args[0]);
                if (mfsDisk.Format == MFS.DiskFormat.Invalid)
                {
                    Console.WriteLine("Error loading RAM file");
                }

                if (args[1].Equals("-d"))
                {
                    //Directory List
                    Console.WriteLine("Directory List:");
                    foreach (MFSEntry entry in mfsDisk.RAMVolume.Entries)
                    {
                        if (entry.GetType() == typeof(MFSDirectory))
                        {
                            Console.WriteLine(((MFSDirectory)entry).DirectoryID + ": " + MFSRAMUtil.GetFullEntryPath(mfsDisk, entry));// + " (" + ((MFSDirectory)entry).CheckShiftJIS() + ")");
                        }
                    }
                }
                else if (args[1].Equals("-f"))
                {
                    //File List
                    Console.WriteLine("File List:");
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
                    //Extract
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
                    //Insert File
                    FileStream testAdd = new FileStream(args[2], FileMode.Open);
                    byte[] testArray = new byte[testAdd.Length];
                    testAdd.Read(testArray, 0, (int)testAdd.Length);
                    testAdd.Close();
                    bool file = false;
                    if (args[3].StartsWith("/") && args[3].EndsWith("/"))
                    {
                        file = MFSRAMUtil.InsertFile(mfsDisk, testArray, Path.GetFileName(args[2]), args[3]);
                    }
                    else if (Regex.IsMatch(args[3], "^\\d+$"))
                    {
                        file = MFSRAMUtil.InsertFile(mfsDisk, testArray, Path.GetFileName(args[2]), ushort.Parse(args[3]));
                    }
                    if (!file)
                        Console.WriteLine("Could not insert file");
                    else
                        Console.WriteLine("File inserted successfully");
                    mfsDisk.Save(args[0] + ".new.ram");
                }
                else if (args[1].Equals("-r"))
                {
                    //Delete (-r emove)
                    if (args.Length > 2)
                    {
                        Console.WriteLine("Delete " + args[2]);
                        if (MFSRAMUtil.DeleteFile(mfsDisk, args[2]))
                        {
                            Console.WriteLine("Done");
                            mfsDisk.Save(args[0] + ".new.ram");
                        }
                        else
                        {
                            Console.WriteLine("Error");
                        }
                    }
                }
                else if (args[1].Equals("-m") && args.Length > 3)
                {
                    //Insert File
                    if (MFSRAMUtil.MoveFile(mfsDisk, args[2], args[3]))
                    {
                        Console.WriteLine("Done");
                        mfsDisk.Save(args[0] + ".new.ram");
                    }
                    else
                        Console.WriteLine("File was not moved/renamed successfully");
                }
            }
        }
    }
}
