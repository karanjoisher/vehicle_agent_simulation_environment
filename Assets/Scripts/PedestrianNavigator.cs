using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class PedestrianNavigator : MonoBehaviour {
    
	// Use this for initialization
	NavMeshAgent agent;
    Vector3 lastAgentVelocity;
    NavMeshPath lastAgentPath;
    bool waitingOnSignal = false;
    int zebraCrossPathMask;
    GameObject lastSeenTrafficSignal;
    
    Renderer color;
    void Start () 
    {
        color = this.GetComponent<Renderer>();
		agent = GetComponent<NavMeshAgent>();
        zebraCrossPathMask = 1 << NavMesh.GetAreaFromName("ZebraCross");
	}
	
	// Update is called once per frame
    
	void Update () 
    {
        TrafficState state = TrafficState.Red;
        RaycastHit hit;
        //Debug.DrawRay(agent.transform.position, agent.transform.forward, Color.yellow, 0.0f, false);
        if(Physics.Raycast(agent.transform.position, agent.transform.forward, out hit, 1.0f))//, DefaultRaycastLayers, QueryTriggerInteraction.Collide))
        {
            //Debug.Log(this.name + " detected " + hit.collider.name + " having tag " + hit.collider.tag);
            if(hit.collider.tag == "TrafficSignal")
            {
                lastSeenTrafficSignal = hit.collider.gameObject;
                state = lastSeenTrafficSignal.GetComponent<TrafficSignal>().state;
            }
        }
        
        if(waitingOnSignal && lastSeenTrafficSignal.GetComponent<TrafficSignal>().state == TrafficState.Red)
        {
            waitingOnSignal = false;
            agent.velocity = lastAgentVelocity;
            agent.SetPath(lastAgentPath);
        }
        else
        {
            NavMeshHit lookAheadPosition, currentPosition;
            agent.SamplePathPosition(~zebraCrossPathMask, 1.0f, out lookAheadPosition);
            agent.SamplePathPosition(~zebraCrossPathMask, 0.01f, out currentPosition);
            
            bool zebraCrossAhead = lookAheadPosition.hit;
            bool alreadyCrossing = currentPosition.hit;
            
            if(state == TrafficState.Green && zebraCrossAhead && !alreadyCrossing)
            {
                lastAgentVelocity = agent.velocity;
                lastAgentPath = agent.path;
                waitingOnSignal = true;
                agent.velocity = Vector3.zero;
                agent.ResetPath();
            }
        }
        
        Color c = Color.white;
        if(waitingOnSignal && state == TrafficState.Red)
        {
            c = Color.red;
        }
        else if(waitingOnSignal && state == TrafficState.Green)
        {
            c = Color.blue;
        }
        else if(!waitingOnSignal && state == TrafficState.Red)
        {
            c = Color.yellow;
        }
        else if(!waitingOnSignal && state == TrafficState.Green)
        {
            c = Color.green;
        }
        
        
        //Color c = waitingOnSignal ? Color.red : Color.green;
        color.material.shader = Shader.Find("_Color");
        color.material.SetColor("_Color", c);
        
        //Find the Specular shader and change its Color to red
        color.material.shader = Shader.Find("Specular");
        color.material.SetColor("_SpecColor", c);
        
    }
}
