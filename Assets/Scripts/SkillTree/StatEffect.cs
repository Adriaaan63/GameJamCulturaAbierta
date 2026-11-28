using System;

public enum StatType
{
    Flashlight,
    SpawnSpeed,
    ProcessRate,
    MaxCapacity,
    Miedo
    // añade los que vayas necesitando
}

[Serializable]
public struct StatEffect
{
    public StatType stat;
    public float value; // se SUMA al valor actual
}