namespace HydroCAD.Models.Network
{
    /// <summary>
    /// Common pipe materials used in sewer and water supply infrastructure.
    /// </summary>
    public enum PipeMaterial
    {
        Unknown,
        PVC,            // Polyvinyl chloride – most common for sewers
        PP,             // Polypropylene
        HDPE,           // High-density polyethylene – pressure pipes
        DuctileIron,    // Ductile cast iron – water supply mains
        Steel,          // Steel – pressure mains
        Concrete,       // Reinforced concrete – large sewers
        VibConcrete,    // Vibrated concrete pipe
        Clay,           // Vitrified clay – older sewers
        GRP,            // Glass-reinforced plastic
    }

    /// <summary>
    /// Pipe purpose within the network.
    /// </summary>
    public enum PipeType
    {
        GravitySewer,       // Sewer flowing by gravity
        PressureSewer,      // Pressure (pumped) sewer
        WaterSupply,        // Drinking water distribution
        StormWater,         // Surface water drainage
        Combined,           // Combined sewer (foul + storm)
    }
}
