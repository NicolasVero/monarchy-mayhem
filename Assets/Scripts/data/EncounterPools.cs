using System;

[Serializable]
public class EncounterPools {
    public EncounterPool[] pools;
}

[Serializable]
public class EncounterPool {
    public string scene;
    public EncounterWeight[] enemies;
}

[Serializable]
public class EncounterWeight {
    public string type;
    public float weight;
}
