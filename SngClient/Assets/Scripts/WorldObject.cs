using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class WorldObject : MonoBehaviour 
{
    public static Dictionary<int, WorldObject> m_worldObjects = new Dictionary<int, WorldObject>();
    public int m_id
    {
        set
        {
            // reassinges to the dictionary
            if (m_id_INTERNAL == value)
                return;
            if (m_id_INTERNAL != 0)
                m_worldObjects.Remove(m_id);
            if (value != 0)
                m_worldObjects.Add(value, this);

            // and this too!
            m_id_INTERNAL = value;
        }
        get
        {
            return m_id_INTERNAL;
        }
    }

    int m_id_INTERNAL = 0;

    // Use this for initialization
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    public void OnDestroy()
    {
        m_id = 0;
    }
}
