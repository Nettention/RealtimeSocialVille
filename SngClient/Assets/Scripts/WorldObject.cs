using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class WorldObject : MonoBehaviour 
{
    public int m_id;

    public void OnDestroy()
    {
        m_id = 0;
    }
}
