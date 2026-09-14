public enum EasternFrontTemplate
{
    OperationTyphoon1941,
    Stalingrad1942,
    Kursk1943,
    OperationBagration1944,
    Sandbox
}

public sealed class ScenarioSettlement
{
    public string Name { get; }
    public double Latitude { get; }
    public double Longitude { get; }
    public int X { get; }
    public int Y { get; }
    public bool IsMajor { get; }

    internal ScenarioSettlement(string name, double latitude, double longitude,
        int x, int y, bool isMajor)
    {
        Name = name;
        Latitude = latitude;
        Longitude = longitude;
        X = x;
        Y = y;
        IsMajor = isMajor;
    }
}

public enum DesignerBrush
{
    Plains,
    Forest,
    Hills,
    Mountains,
    Water,
    River,
    Ford,
    Desert,
    RedSide,
    BlueSide,
    Neutral
}
