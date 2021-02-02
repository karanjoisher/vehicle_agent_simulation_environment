using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum TrafficState
{
    Red,
    Green,
    NumStates,
};
public class TrafficSignal : MonoBehaviour 
{
    public TrafficState state = TrafficState.Red;
    Color []colors = {Color.red, Color.green};
    public float signalStateDuration = 2.0f;
    float durationSpentInCurrentState = 0.0f;
    Renderer light;
	// Use this for initialization
	void Start () 
    {
		light = this.GetComponent<Renderer>();
        Color c = colors[(int)state];
        
        light.material.shader = Shader.Find("_Color");
        light.material.SetColor("_Color", c);
        
        //Find the Specular shader and change its Color to red
        light.material.shader = Shader.Find("Specular");
        light.material.SetColor("_SpecColor", c);
	}
	
	// Update is called once per frame
	void Update () 
    {
        durationSpentInCurrentState += Time.deltaTime;
		if(durationSpentInCurrentState >= signalStateDuration)
        {
            durationSpentInCurrentState = 0.0f;
            state = (TrafficState)(((int)state + 1) % (int)TrafficState.NumStates);
            Color c = colors[(int)state];
            
            light.material.shader = Shader.Find("_Color");
            light.material.SetColor("_Color", c);
            
            //Find the Specular shader and change its Color to red
            light.material.shader = Shader.Find("Specular");
            light.material.SetColor("_SpecColor", c);
        }
	}
}
