using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using UnityStandardAssets.Vehicles.Car;



public class CarControllerSandbox : MonoBehaviour
{
    // Use this for initialization
    GameObject car;
    NavMeshAgent agent;
    public Transform destination;
    CarAIControl carAIController;
    CarController carController;
    
    public float speed;
    public float steeringAngle;
    public float steering;
    public float handbrake;
    public float turningFactor;
    public Text debugUI;
    
    int zebraCrossPathMask;
    Vector3 lastAgentVelocity;
    NavMeshPath lastAgentPath;
    
    public bool waitingOnSignal = false;
    public bool draw = false;
    public bool alreadyCrossing = false;
    public bool zebraCrossAhead = false;
    public GameObject signal = null;
    void Awake()
    {
        //Debug.Log("Hey");
        car = this.gameObject;
        carAIController = car.GetComponent<CarAIControl>();
        carController = car.GetComponent<CarController>();
        
        agent = car.GetComponentInChildren<NavMeshAgent>();
        agent.transform.localPosition = new Vector3();
        agent.transform.rotation = new Quaternion();
        
        if(destination == null) destination = car.transform;
        /*agent.SetDestination(destination.position);
        zebraCrossPathMask = 1 << NavMesh.GetAreaFromName("ZebraCross");
        */
        
        if(carController.m_SpeedType == SpeedType.KPH)
        {
            agent.speed = carController.m_Topspeed * 0.447f;
        }
        else if(carController.m_SpeedType == SpeedType.MPH)
        {
            agent.speed = carController.m_Topspeed * 0.278f;
        }
        
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
    // Update is called once per frame
    void Foo()
    {
        agent.transform.localPosition = new Vector3();
        agent.transform.rotation = new Quaternion();
        
        TrafficState state = TrafficState.Green;
        Collider[] colliders = Physics.OverlapSphere(agent.transform.position, 20.0f);
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
        
        
        if (waitingOnSignal || Vector3.Distance(car.transform.position, destination.position) <= agent.stoppingDistance) 
        {
            steering = 0.0f;
            speed = 0.0f;
            handbrake = 1.0f; 
        }
        else
        {
            Vector2 to = new Vector2(agent.transform.forward.x, agent.transform.forward.z);
            Vector2 from = new Vector2(agent.desiredVelocity.x, agent.desiredVelocity.z);
            from = from.normalized;
            to = to.normalized;
            
            turningFactor = Vector2.Dot(from, to);
            turningFactor = Mathf.Sign(turningFactor) * Mathf.Clamp(Mathf.Abs(turningFactor), 0.35f, 1.0f);
            
            speed = agent.desiredVelocity.magnitude * turningFactor;
            speed = speed / agent.speed;
            
            steeringAngle = Vector2.SignedAngle(from, to);
            steering = RemapRange(-carController.m_MaximumSteerAngle, carController.m_MaximumSteerAngle, -1, 1, steeringAngle);
            if (turningFactor < 0.0f) steering = steering * -1.0f;
            handbrake = 0.0f;
        }
        
        car.GetComponent<CarController>().Move(steering, speed, speed, handbrake);
        
        if(draw)
        {
            DebugDrawPath(agent.path);
            Debug.DrawRay(agent.transform.position, agent.desiredVelocity, Color.cyan, 0.0f, false);
            Debug.DrawRay(agent.transform.position, agent.transform.forward * Vector3.Magnitude(agent.desiredVelocity), Color.yellow, 0.0f, false);
        }
    }
    
    bool startedBraking = false;
    bool done  = false;
    Vector3 start = new Vector3();
    Vector3 end = new Vector3();
    float speedAtBraking = 0.0f;
    
    public float topSpeedKMPH = 40.0f;
    public float speedIncrementKMPH = 5.0f;
    Vector3 reset;
    
    float[] breakingDistances;
    public float[] speeds;
    public float[] calculated;
    bool firstFrame = true;
    
    public float currentSpeed = 0.0f;
    int index = 0;
    
    float CalculateBrakeDistance(float speed)
    {
        float square = speed * speed;
        float cube = square * speed;
        float four = cube * speed;
        float five = four * speed;
        float six = five * speed;
        float seven = six * speed;
        //return (-0.000122202f*seven) + (0.00541548f * six) + (-0.0979168f*five) + (0.929088f*four) + (-4.95129f*cube) + (14.7349f*square) + (-21.9993f*speed) + 12.676f;
        
        return 0.0663518f*square + 0.102262f*speed - 0.15791f;
    }
    void Bar()
    {
        if(firstFrame)
        {
            firstFrame = false;
            reset = car.transform.position;
            currentSpeed = speedIncrementKMPH;
            breakingDistances = new float[(int)((topSpeedKMPH + speedIncrementKMPH)/speedIncrementKMPH)];
            speeds = new float[(int)((topSpeedKMPH + speedIncrementKMPH)/speedIncrementKMPH)];
            calculated = new float[(int)((topSpeedKMPH + speedIncrementKMPH)/speedIncrementKMPH)];
        }
        else if(index < breakingDistances.Length)
        {
            if(!startedBraking && carController.m_Rigidbody.velocity.magnitude * 3.6f >= currentSpeed)
            {
                start = car.transform.position;
                speeds[index] = carController.m_Rigidbody.velocity.magnitude;
                startedBraking = true;
            }
            
            if(startedBraking && carController.m_Rigidbody.velocity.z <= 0.0f)
            {
                end = car.transform.position;
                done = true;
            }
            
            if(!done && startedBraking) carController.Move(0.0f, 0.0f, -1.0f, 1.0f);
            
            if(!done && !startedBraking) carController.Move(0.0f, 1.0f, 0.0f, 0.0f);
            if(done) 
            {
                calculated[index] = CalculateBrakeDistance(speeds[index]);
                breakingDistances[index] = Vector3.Distance(start, end);
                currentSpeed += speedIncrementKMPH;
                startedBraking = false;
                done = false;
                car.transform.position = reset;
                carController.m_Rigidbody.velocity = Vector3.zero;
                index++;
            }
        }
        else if(index == breakingDistances.Length)
        {
            index++;
            string csv = "Speed(m/s), Distance(m), Calculated(m)\n";
            for(int i = 0; i < breakingDistances.Length; i++)
            {
                string s = speeds[i] + "," + breakingDistances[i] + "," +calculated[i] + "\n";
                
                csv += s;
            }
            
            Debug.Log(csv);
        }
        
        debugUI.text = "" + carController.m_Rigidbody.velocity * 3.6f;
    }
    
    void Foo1()
    {
        if(!done && carController.m_Rigidbody.velocity.magnitude * 3.6f < 40.0f)
        {
            carController.Move(0.0f, 1.0f, 0.0f, 0.0f);
        }
        else if(!done)
        {
            reset = carController.transform.position;
            done = true;
        }
        
        if(done) 
        {
            carController.m_Rigidbody.velocity = Vector3.zero;
            car.transform.position = reset;
        }
    }
    
    public int collidersInTrigger = 0; 
    private void OnTriggerEnter(Collider other)
    {
        if(other.tag == "Stop")
        {
            collidersInTrigger++;
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if(other.tag == "Stop")
        {
            collidersInTrigger--;
            if(collidersInTrigger < 0) collidersInTrigger = 0;
        }
        
    }
    
    void Update()
    {
        if(collidersInTrigger == 0)
        {
            startedBraking = false;
            carController.Move(0.0f, 1.0f, 0.0f, 0.0f);
        }
        else if(!startedBraking)
        {
            startedBraking = true;
            reset = carController.transform.position;
        }
        
        if(startedBraking)
        {
            carController.m_Rigidbody.velocity = Vector3.zero;
            carController.m_Rigidbody.angularVelocity = Vector3.zero;
            car.transform.position = reset;
        }
    }
    
}
