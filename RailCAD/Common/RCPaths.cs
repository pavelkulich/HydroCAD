using System;


namespace RailCAD.Common
{
    internal class RCPaths
    {
        public static string GetAppDataPath()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string rcAppDataPath = System.IO.Path.Combine(appDataPath, "RailCAD");

            if (!System.IO.Directory.Exists(rcAppDataPath))
            {
                System.IO.Directory.CreateDirectory(rcAppDataPath);
            }

            return rcAppDataPath;
        }
    }
}
