using System;
using System.Collections.Generic;
using UnityEngine;

// every map a run can take place on, in menu order. the first one plays when nothing has been
// picked. lives in Resources so the menu and the game scene both find it without wiring
[CreateAssetMenu(menuName = "Rogue/Map Catalog")]
public class MapCatalog : ScriptableObject
{
    public const string ResourcePath = "MapCatalog";

    [Serializable]
    public class Map
    {
        public string title;
        [TextArea] public string subtitle;
        [Tooltip("the playfield prefab: its art, walls and spawn timeline")]
        public Playfield playfield;
        [Tooltip("optional. a picture for the map picker")]
        public Sprite preview;
    }

    public List<Map> maps = new List<Map>();

    private static MapCatalog cached;

    public static MapCatalog Load()
    {
        if (cached == null) cached = Resources.Load<MapCatalog>(ResourcePath);
        return cached;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => cached = null;
}
