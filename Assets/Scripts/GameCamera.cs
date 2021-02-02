using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameCamera : MonoBehaviour {


    public Transform player;
    public float seconds_delay;
    float prev_angle;

    private int prev_direction = 0;

    Vector3 prev_position;

    Vector3 offset;
	// Use this for initialization
	void Start () {
        offset = transform.position - player.position;
	}
	
	// Update is called once per frame
	void Update () {

        Debug.Log("============================");
        Debug.Log(offset);
        Debug.Log(transform.position);


        //transform.position = player.position + offset;

        float ydiff = player.rotation.y - transform.rotation.y;
        float tdiff = (player.position.z - (transform.position.z - offset.z));

        transform.RotateAround(player.position, Vector3.up, ydiff);


	}
}
