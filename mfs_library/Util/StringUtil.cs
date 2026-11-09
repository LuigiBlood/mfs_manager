using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mfs_library
{
    public static class StringUtil
    {
        public static string ReplaceInvalidChars(string filename)
        {
            return string.Join("_", filename.Split(Path.GetInvalidFileNameChars()));
        }

        public static string GetFileNameWithoutExtension(string filename)
        {
            var temp = filename.Split('.').ToList();
            temp.RemoveAt(temp.Count - 1);
            return string.Join(".", temp);
        }

        public static string GetExtension(string filename)
        {
            var temp = filename.Split('.');
            return "." + temp[temp.Length - 1];
        }
    }
}
