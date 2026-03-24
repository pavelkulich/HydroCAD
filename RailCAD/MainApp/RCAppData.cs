using System.Collections.Generic;
using System.Windows.Controls.Primitives;
using RailCAD.Models.TerrainModel;

namespace RailCAD.MainApp
{
    internal class RCAppData
    {
        private static RCAppData _instance;
        private static readonly object _lock = new object();

        private Dictionary<string, TerrainModel> terrainModels = new Dictionary<string, TerrainModel>();

        private RCAppData() { }

        // Singleton pattern: ensure only one instance of RCAppData exists
        public static RCAppData Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new RCAppData();
                    }
                    return _instance;
                }
            }
        }

        public TerrainModel GetTerrainModel(string appName)
        {
            return terrainModels.ContainsKey(appName) ? terrainModels[appName] : null;
        }

        public void SetTerrainModel(TerrainModel value)
        {
            this.terrainModels[value.Name] = value;
        }
    }
}
