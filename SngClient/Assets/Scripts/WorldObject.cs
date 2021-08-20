using UnityEngine;

public class WorldObject : MonoBehaviour
{
    public int m_id;

    public void OnDestroy()
    {
        m_id = 0;
    }
}
