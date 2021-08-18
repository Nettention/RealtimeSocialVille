using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class WorldObject : MonoBehaviour 
{
    public int m_id;

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
