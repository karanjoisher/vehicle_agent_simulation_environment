using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class TrafficDemo : MonoBehaviour 
{
    public bool on = true;
    public string[] laneNames = {"Lane1", "Lane2"};
    GameObject carAgentTemplate;
    public GameObject [][]laneWaypoints;
    
    GameObject []cars;
    public int []carWaypointIndex;
    public int []carLaneIndex;
    
    public float threshold = 4.0f;
	// Use this for initialization
	void Start () 
    {
        if(on)
        {
            int numLanes = laneNames.Length;
            laneWaypoints = new GameObject[numLanes][];
            
            int totalWaypoints = 0;
            for(int k = 0; k < numLanes; k++)
            {
                GameObject lane = GameObject.Find(laneNames[k]);
                int numWaypoints = lane.transform.childCount;
                totalWaypoints += numWaypoints;
                laneWaypoints[k] = new GameObject[numWaypoints];
                
                for(int i = 1; i <= numWaypoints; i++)
                {
                    laneWaypoints[k][i-1] = GameObject.Find(laneNames[k] + "/" + i);
                    Debug.Assert(laneWaypoints[k][i-1] != null);
                }
            }
            
            carAgentTemplate = GameObject.Find("AICarTemplate");
            cars = new GameObject[totalWaypoints];
            carWaypointIndex = new int[totalWaypoints];
            carLaneIndex= new int[totalWaypoints];
            
            int carIndex = 0;
            for(int k = 0; k < numLanes; k++)
            {
                int numWaypoints = laneWaypoints[k].Length;
                for(int i = 0; i < numWaypoints; i++)
                {
                    cars[carIndex] = (GameObject) Instantiate(carAgentTemplate, laneWaypoints[k][i].transform.position, laneWaypoints[k][i].transform.rotation);
                    cars[carIndex].gameObject.name = "car" + carIndex;
                    carWaypointIndex[carIndex] = i; 
                    carLaneIndex[carIndex] = k; 
                    cars[carIndex].GetComponent<CarNavigator>().SetDestination(laneWaypoints[k][(i+1) % numWaypoints].transform);
                    carIndex = carIndex + 1;
                }
            }
            Destroy(carAgentTemplate);
        }
    }
    
    
    
    
	// Update is called once per frame
	void Update () 
    {
        if(on)
        {
            for(int i = 0; i < cars.Length; i++)
            {
                int laneIndex = carLaneIndex[i];
                int destinationWaypointIndex = (carWaypointIndex[i] + 1) % laneWaypoints[laneIndex].Length;
                if(Vector3.Distance(cars[i].transform.position, laneWaypoints[laneIndex][destinationWaypointIndex].transform.position) <= threshold)
                {
                    carWaypointIndex[i] = destinationWaypointIndex;
                    destinationWaypointIndex = (destinationWaypointIndex + 1) % laneWaypoints[laneIndex].Length;
                    cars[i].GetComponent<CarNavigator>().SetDestination(laneWaypoints[laneIndex][destinationWaypointIndex].transform);
                }
            }
        }
        
        /*
  foreach(GameObject i in laneWaypoints)
        {
            Debug.DrawRay(i.transform.position, i.transform.forward * 4.0f, Color.red, 0.0f, false);
        }
        */
	}
    
}
