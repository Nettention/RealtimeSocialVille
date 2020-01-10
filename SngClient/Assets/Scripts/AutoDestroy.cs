using UnityEngine;
using System.Collections;

public class AutoDestroy : MonoBehaviour {

	// Use this for initialization
	void Start () {
        Destroy(gameObject, 1); // auto delete after 1 sec.
	}
	
	// Update is called once per frame
	void Update () {
	
	}
}
