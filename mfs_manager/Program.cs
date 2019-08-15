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
            Console.WriteLine("64DD MFS Manager");
            if (args.Length == 1)
            {
                MFSDisk test = new MFSDisk(args[0]);
                if (test.Format == MFS.DiskFormat.Invalid)
                    Console.WriteLine("Error");

                foreach (MFSEntry entry in test.RAMVolume.Entries)
                {
                    if (entry.GetType() == typeof(MFSDirectory))
                    {
                        Console.WriteLine(entry.Name);
                    }
                    else
                    {
                        Console.WriteLine(entry.Name + "." + ((MFSFile)entry).Ext);
                        byte[] data = test.ReadFileData((MFSFile)entry);
                        FileStream fileExt = new FileStream(".\\extract\\" + entry.Name + "." + ((MFSFile)entry).Ext, FileMode.Create);
                        fileExt.Write(data, 0, data.Length);
                        fileExt.Close();
                    }
                }
            }
        }
    }
}
