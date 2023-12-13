using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SentDown : MonoBehaviour
{
    public Transform playerpos;
    public Collider caveentrancecollider;

    // Start is called before the first frame update
    void Start()
    {
        playerpos = GameObject.Find("Player").transform;
        caveentrancecollider = GetComponent<Collider>();
    }

    void OnTriggerEnter(Collider cavecollider)
    {
        playerpos.transform.position = new Vector3(playerpos.position.x,playerpos.position.y - 100,playerpos.position.z);
    }

    // Update is called once per frame
    void Update()
    {
        Debug.Log(playerpos.position.x+" "+playerpos.position.y+" "+playerpos.position.z);
    }
}
