using UnityEngine;

public static class Layers
{
    public static string GroundName = "Ground";
    public static string AirName = "Air";
    public static string WallName = "Wall";
    public static string CatName = "Cat";

    public static int Ground => LayerMask.NameToLayer(GroundName);
    public static int Air => LayerMask.NameToLayer(AirName);
    public static int Wall => LayerMask.NameToLayer(WallName);
    public static int Cat => LayerMask.NameToLayer(CatName);
}