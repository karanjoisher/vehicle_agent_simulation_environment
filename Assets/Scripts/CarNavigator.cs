using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityStandardAssets.Vehicles.Car;

public class CarNavigator : MonoBehaviour
{
    // Use this for initialization
    GameObject car;
    NavMeshAgent agent;
    public Transform destination;
    CarController carController;
    
    public float speed = 0.0f; 
    public float steeringAngle = 0.0f;
    public float steering = 0.0f; // normalized steeringAngle
    public float handbrake = 0.0f; 
    public float turningFactor = 0.0f; 
    
    public bool drawVisualInfo = false;
    
    public int collidersInTrigger = 0;
    int zebraCrossPathMask;
    public bool signalToStop = false;
    public bool somethingTooClose = false;
    public bool reachedDestination = false;
    Vector3 pausePosition = new Vector3();
    
    void Awake()
    {
        car = this.gameObject;
        pausePosition = car.transform.position;
        carController = car.GetComponent<CarController>();
        
        agent = car.GetComponentInChildren<NavMeshAgent>();
        agent.transform.localPosition = new Vector3();
        agent.transform.rotation = new Quaternion();
        
        if(carController.m_SpeedType == SpeedType.KPH)
        {
            agent.speed = carController.m_Topspeed * 0.447f;
        }
        else if(carController.m_SpeedType == SpeedType.MPH)
        {
            agent.speed = carController.m_Topspeed * 0.278f;
        }
        
        if(destination == null) destination = car.transform;
        //agent.SetDestination(destination.position);
        zebraCrossPathMask = 1 << NavMesh.GetAreaFromName("ZebraCross");
    }
    
    public void SetDestination(Transform targetDestination)
    {
        destination = targetDestination;
        agent.SetDestination(destination.position);
    }
    
    void DebugDrawPath(NavMeshPath path)
    {
        for (int i = 0; i < path.corners.Length - 1; i++)
        {
            Debug.DrawLine(path.corners[i], path.corners[i + 1], Color.red, 0.0f, false);
        }
    }
    
    float RemapRange(float initialStart, float initialEnd, float newStart, float newEnd, float value)
    {
        float result;
        result = (((value - initialStart) / (initialEnd - initialStart)) * (newEnd - newStart)) + newStart;
        return result;
    }
    
    
    void OnTriggerEnter(Collider other)
    {
        //Debug.Log("Enter: " + other.gameObject.name + "| Tag: " + other.tag + ", " + other.gameObject.tag);
        if(other.tag == "Pedestrian" || other.tag == "Car")
        {
            collidersInTrigger++;
        }
    }
    
    void OnTriggerExit(Collider other)
    {
        //Debug.Log("Exit: " + other.gameObject.name + "| Tag: " + other.tag + ", " + other.gameObject.tag);
        if(other.tag == "Pedestrian" || other.tag == "Car")
        {
            collidersInTrigger--;
            if(collidersInTrigger < 0) collidersInTrigger = 0;
        }
    }
    
    void BrakeOnTheSpot()
    {
        steering = 0.0f;
        speed = 0.0f;
        handbrake = 1.0f;
        turningFactor = 0.0f;
        carController.m_Rigidbody.velocity = Vector3.zero;
        carController.m_Rigidbody.angularVelocity = Vector3.zero;
        car.transform.position = pausePosition;
    }
    
    // Update is called once per frame
    void Update()
    {
        agent.transform.localPosition = new Vector3();
        agent.transform.rotation = new Quaternion();
        
        // Check whether there is a traffic signal at front
        signalToStop = false;
        RaycastHit hit;
        
        //Debug.DrawRay(car.transform.position, car.transform.forward * 5.0f, Color.white, 0.0f, false);
        if(Physics.Raycast(car.transform.position, car.transform.forward, out hit, 5.0f))//, DefaultRaycastLayers, QueryTriggerInteraction.Collide))
        {
            //Debug.Log(this.name + " detected " + hit.collider.name + " having tag " + hit.collider.tag);
            if(hit.collider.tag == "TrafficSignal")
            {
                GameObject trafficSignal = hit.collider.gameObject;
                
                NavMeshHit currentPosition;
                agent.SamplePathPosition(~zebraCrossPathMask, 0.01f, out currentPosition);
                bool alreadyCrossing = currentPosition.hit;
                
                signalToStop = alreadyCrossing ? false : trafficSignal.GetComponent<TrafficSignal>().state == TrafficState.Red;
            }
        }
        
        //Check whether there is any collider too close to the car
        somethingTooClose = collidersInTrigger > 0;
        
        //Check whether the car has reached destination
        reachedDestination = Vector3.Distance(car.transform.position, destination.position) <= agent.stoppingDistance;
        
        if(signalToStop)
        {
            BrakeOnTheSpot();
        }
        else if(somethingTooClose)
        {
            BrakeOnTheSpot();
        }
        else if(reachedDestination)
        {
            BrakeOnTheSpot();
        }
        else // Free to move
        {
            Vector2 to = new Vector2(agent.transform.forward.x, agent.transform.forward.z);
            Vector2 from = new Vector2(agent.desiredVelocity.x, agent.desiredVelocity.z);
            
            from = from.normalized;
            to = to.normalized;
            
            turningFactor = Vector2.Dot(from, to);
            
            // Don't allow turning factor to be 0 or very small
            turningFactor = Mathf.Sign(turningFactor) * Mathf.Clamp(Mathf.Abs(turningFactor), 0.35f, 1.0f);
            
            // If turning factor magnitude is greater than 0.5,
            // and it is negative, it means that we need to go in
            // reverse as we cannot take such a sharp turn.
            // Else the turning factor is kept positive, meaning 
            // that we'll try to make that turn without going into reverse 
            if(Mathf.Abs(turningFactor) < 0.5f)
            {
                turningFactor = Mathf.Abs(turningFactor);
            }
            
            //Sharper the turn, smaller the speed.
            speed = agent.desiredVelocity.magnitude * turningFactor;
            speed = speed / agent.speed; // normalize
            
            steeringAngle = Vector2.SignedAngle(from, to);
            steering = RemapRange(-carController.m_MaximumSteerAngle, carController.m_MaximumSteerAngle, -1, 1, steeringAngle); // normalize
            
            if (turningFactor < 0.0f) steering = steering * -1.0f; //if steering in reverse
            
            handbrake = 0.0f;
            
            carController.Move(steering, speed, speed, handbrake);
            pausePosition = car.transform.position;
        }
        
        
        // Update the position of threat detection depending on whether we are going forward or reverse
        Vector3 safetyCushionPos  = car.transform.Find("SafetyCushion").localPosition;
        car.transform.Find("SafetyCushion").localPosition = new Vector3(safetyCushionPos.x, safetyCushionPos.y, Mathf.Sign(turningFactor) * Mathf.Abs(safetyCushionPos.z)); 
        
        if(drawVisualInfo)
        {
            DebugDrawPath(agent.path);
            Debug.DrawRay(agent.transform.position, agent.desiredVelocity, Color.cyan, 0.0f, false);
            Debug.DrawRay(agent.transform.position, agent.transform.forward * Vector3.Magnitude(agent.desiredVelocity), Color.yellow, 0.0f, false);
        }
        
    }
}



/*
agent.transform.localPosition = new Vector3();
agent.transform.rotation = new Quaternion();


TrafficState state = TrafficState.Green;
Collider[] colliders = Physics.OverlapSphere(agent.transform.position, 10.0f);
for(int i = 0; i < colliders.Length; i++)
{
    if(colliders[i].tag == "TrafficSignal")
    {
        state = colliders[i].gameObject.GetComponent<TrafficSignal>().state;
        signal = colliders[i].gameObject;
        break;
    }
}

waitingOnSignal = state == TrafficState.Red;
if(signal && Vector3.Dot(signal.transform.position - car.transform.position, car.transform.forward) < 0.0f)
{
    waitingOnSignal = false;
}

NavMeshHit currentPosition;
agent.SamplePathPosition(~zebraCrossPathMask, 0.01f, out currentPosition);
bool alreadyCrossing = currentPosition.hit;

if(alreadyCrossing) waitingOnSignal = false;

if(!stop) pausePosition = carController.transform.position;
stop = reached || startedBraking || waitingOnSignal;

if (stop) 
{
    carController.m_Rigidbody.velocity = Vector3.zero;
    carController.m_Rigidbody.angularVelocity = Vector3.zero;
    car.transform.position = pausePosition;
    turningFactor = 0.0f;
    //steering = 0.0f;
    //speed = 0.0f;
    //handbrake = 1.0f; 
}
else
{
    Vector2 to = new Vector2(agent.transform.forward.x, agent.transform.forward.z);
    Vector2 from = new Vector2(agent.desiredVelocity.x, agent.desiredVelocity.z);
    from = from.normalized;
    to = to.normalized;
    
    turningFactor = Vector2.Dot(from, to);
    turningFactor = Mathf.Sign(turningFactor) * Mathf.Clamp(Mathf.Abs(turningFactor), 0.35f, 1.0f);
    
    if(Mathf.Abs(turningFactor) < 0.5f)
    {
        turningFactor = Mathf.Abs(turningFactor);
    }
    
    speed = agent.desiredVelocity.magnitude * turningFactor;
    speed = speed / agent.speed;
    
    steeringAngle = Vector2.SignedAngle(from, to);
    steering = RemapRange(-carController.m_MaximumSteerAngle, carController.m_MaximumSteerAngle, -1, 1, steeringAngle);
    
    if (turningFactor < 0.0f) steering = steering * -1.0f;
    handbrake = 0.0f;
    
    
    car.GetComponent<CarController>().Move(steering, speed, speed, handbrake);
}
Vector3 foo = car.transform.Find("SafetyCushion").localPosition;

car.transform.Find("SafetyCushion").localPosition = new Vector3(foo.x, foo.y, Mathf.Sign(turningFactor) * Mathf.Abs(foo.z)); 


}
}
*/
