using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class NPCExperimentation : MonoBehaviour 
{
    public bool stop = true;
    public bool zebraCrossAhead = false;
    public bool alreadyCrossing = false;
    
    public float signalStateDuration = 2.0f;
    float durationSpentInCurrentState = 0.0f;
    public bool waitingOnSignal = false;
    int zebraCrossPathMask;
	
    Vector3 [] positions;
    int targetIndex = 0;
    
    NavMeshAgent agent;
    Vector3 lastAgentVelocity;
    NavMeshPath lastAgentPath;
    
    // Use this for initialization
	void Start () 
    {
        agent = GetComponent<NavMeshAgent>();
        positions = new Vector3[2];
		positions[0] = transform.position;
        string otherNPCName = "NPC1";
        if(this.name == "NPC1")
        {
            otherNPCName = "NPC2";
        }
        
        zebraCrossPathMask = 1 << NavMesh.GetAreaFromName("ZebraCross");
        
        positions[1] = GameObject.Find(otherNPCName).transform.position;
	}
	
    
    public static string GetStringFromAreaMask(int mask)
    {
        string result = "not found";
        if((mask & (1 << NavMesh.GetAreaFromName("ZebraCross"))) != 0)
        {
            result = "ZebraCross";
        }
        if((mask & (1 << NavMesh.GetAreaFromName("Walkable"))) != 0)
        {
            result = "Walkable";
        }
        if((mask & (1 << NavMesh.GetAreaFromName("Non Walkable"))) != 0)
        {
            result = "Non Walkable";
        }
        if((mask & (1 << NavMesh.GetAreaFromName("Jump"))) != 0)
        {
            result = "Jump";
        }
        
        return result;
    }
	// Update is called once per frame
	void Update () 
    {
        durationSpentInCurrentState += Time.deltaTime;
        if(durationSpentInCurrentState >= signalStateDuration)
        {
            durationSpentInCurrentState = 0.0f;
            stop = !stop;
            
            //Fetch the Renderer from the GameObject
            Renderer rend = GameObject.Find("TrafficSignal").GetComponent<Renderer>();
            
            //Set the main Color of the Material to green
            Color c = stop ? Color.red : Color.green;
            rend.material.shader = Shader.Find("_Color");
            rend.material.SetColor("_Color", c);
            
            //Find the Specular shader and change its Color to red
            rend.material.shader = Shader.Find("Specular");
            rend.material.SetColor("_SpecColor", c);
        }
        
        
        NavMeshHit lookAheadPosition, currentPosition;
        agent.SamplePathPosition(~zebraCrossPathMask, 1.0f, out lookAheadPosition);
        agent.SamplePathPosition(~zebraCrossPathMask, 0.01f, out currentPosition);
        
        zebraCrossAhead = lookAheadPosition.hit;
        alreadyCrossing = currentPosition.hit;
        
        if(stop && zebraCrossAhead && !alreadyCrossing)
        {
            lastAgentVelocity = agent.velocity;
            lastAgentPath = agent.path;
            waitingOnSignal = true;
            agent.velocity = Vector3.zero;
            agent.ResetPath();
        }
        
        if(!stop && waitingOnSignal)
        {
            waitingOnSignal = false;
            agent.velocity = lastAgentVelocity;
            agent.SetPath(lastAgentPath);
        }
        
        if(Vector3.Distance(transform.position, positions[targetIndex]) < 0.9f)
        {
            targetIndex = (targetIndex + 1) % positions.Length;
            agent.destination = positions[targetIndex];
        }
	}
    
    
}
