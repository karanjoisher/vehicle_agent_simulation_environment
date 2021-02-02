using UnityEngine;
using UnityEngine.UI;

public class VehicleSimulator : MonoBehaviour {
    
    //UI Components
    
    public Text speedText;
    public Text totalForceText;
    public Text driveForceText;
    public Text rollingResText;
    public Text dragText;
    public Text torqueText;
    public Text steerText;
    
    
    /*
     * Assumptions:
     *  Completely Flat Ground
     *  Perfect Weight Balance
     *  No wieght shift when accelerating
     *  No Slip of rear wheels
     *  No drift while turning
     */
    
    public AnimationCurve rpmTorqueGraph; //used to define a from rpm to max torque
    
    
    public float maxSteeringAngle; //max steering angle in degrees
    
    public float staticFrictionCoeff;
    public float dynamicFrictionCoeff;
    public float rollingResistanceCoeff;
    public float dragCoeff;
    public float corneringStiffness;
    
    public float wheelRadius;
    public float mass;
    public float frontalArea;
    public float densityOfAir;
    public float frontOffsetFromCOM; //offset of front wheels from the center of mass
    public float rearOffsetFromCOM; //offset of rear wheels from the center of mass
    public float minRPM;
    public float maxRPM;
    
    public float torqueMultiplier;
    public float transmissionEfficiency;
    
    private float throttle; //0 to 1 how much throttle is pressed, controls the rpm
    private float brake;
    
    private float steeringRatio; //-1 to 1 ratio of how much the steering wheel is turned
    
    private Rigidbody rb;
    
    /*
     * Formulas Used:
     * Get max torque from the graph using current rpm
     * Get actual torque using max_torque * torque_mult * efficiency
     * Get forward force applied = actual_torque / radius
     * Clutch and gears not added
    */
    
    
    
	void Start () {
        rb = GetComponent<Rigidbody>();
        rb.mass = mass;
	}
    
    void OLD()
    {
        Vector3 velocity = rb.velocity;
        
        throttle = Mathf.Clamp(Input.GetAxis("Vertical"), 0f, 1f); // no brakes right now
        
        
        
        float currentRPM = maxRPM * throttle;
        
        float torque = rpmTorqueGraph.Evaluate(currentRPM) * torqueMultiplier * transmissionEfficiency / 100f;
        
        float forwardForce = torque / wheelRadius;
        
        //when considering rolling add limiting force arising due to friction
        
        float rollingResistance = rollingResistanceCoeff * velocity.magnitude;
        float airResistance = 0.5f * dragCoeff * frontalArea * densityOfAir * velocity.sqrMagnitude;
        
        Debug.Log(forwardForce + ", " + rollingResistance + ", " + airResistance);
        
        Vector3 totalForce = transform.forward * (forwardForce - rollingResistance - airResistance);
        
        Debug.DrawRay(transform.position, transform.forward * 10f, Color.green);
        Debug.DrawRay(transform.position, transform.right * 10f, Color.red);
        
        
        rb.AddForce(totalForce);
        
        steeringRatio = Mathf.Clamp(Input.GetAxis("Horizontal"), -1, 1);
        
        if (!Mathf.Approximately(steeringRatio, 0))
        {
            float steeringAngle = steeringRatio * maxSteeringAngle;
            
            
            float l = frontOffsetFromCOM + rearOffsetFromCOM; //distance between front and rear wheels
            
            float r = (l / Mathf.Sin(steeringAngle * Mathf.Deg2Rad));
            
            float angularVelocity = velocity.magnitude / r;
            
            float rotationChange = angularVelocity * Time.deltaTime; //rotation change in radians
            
            Debug.Log(velocity.magnitude);
            
            
            transform.Rotate(transform.up, rotationChange * Mathf.Rad2Deg);
            
            float centrepetalForce = Mathf.Abs(mass * velocity.sqrMagnitude / r);
            
            rb.AddForce(transform.right * centrepetalForce * Mathf.Sign(steeringAngle));
        }
        
    }
    
    // Update is called once per frame
    
    //z is front 
    
    void FixedUpdate ()
    {
        //get throttle (0 to 1) and steering angle (-1 to 1) from user
        throttle = Mathf.Clamp(Input.GetAxis("Vertical"), 0f, 1f);
        steeringRatio = -Mathf.Clamp(Input.GetAxis("Horizontal"), -1, 1);
        
        float steeringAngle = steeringRatio * maxSteeringAngle; //mapping -1 to 1 -> -maxAngle to +maxAngle
        
        Vector3 localVelocity = transform.InverseTransformDirection(rb.velocity); //getting local veloctiy
        float localSpeed = localVelocity.magnitude; //getting speed from velocity
        float longVel = localVelocity.z; //define lateral (right) longitudinal (front) and up velocity
        float latVel = localVelocity.x;
        float upVel = localVelocity.y;
        
        //distance between front and rear
        float lengthOfCar = rearOffsetFromCOM + frontOffsetFromCOM; 
        
        //define angular velocity and speed
        Vector3 localAngularVelocity = transform.InverseTransformDirection(rb.angularVelocity); 
        float localAngularSpeed = localAngularVelocity.magnitude;
        
        // use weight on front or rear wheels = (distance to the wheel from centre of mass / distance between the front and rear wheels) * weight
        //TODO(suhaib): change this to include weight shift as well as gravity on slopes
        float forceOnRearWheel = (rearOffsetFromCOM / lengthOfCar) * mass * Physics.gravity.magnitude; 
        float forceOnFrontWheel = (frontOffsetFromCOM / lengthOfCar) * mass * Physics.gravity.magnitude;
        
        //front wheel (fw) slip angle = tan-1(side velocity + fw linear vel, forward vel) - steering angle in radians.
        //back wheel same except the steering angle part
        //linear velocity = angular vel * distance from COM
        float slipAngleFront = Mathf.Atan2(latVel + localAngularSpeed * frontOffsetFromCOM, longVel) - steeringAngle * Mathf.Deg2Rad * Mathf.Sign(longVel);
        
        
        float slipAngleRear = Mathf.Atan2(latVel + localAngularSpeed * rearOffsetFromCOM, longVel);
        
        float latForceFront = corneringStiffness * slipAngleFront * forceOnFrontWheel;
        float latForceRear = corneringStiffness * slipAngleRear * forceOnRearWheel;
        
        float maxLatForceFront = staticFrictionCoeff * forceOnFrontWheel; //change to consider dynamic friction as well
        float maxLatForceRear = staticFrictionCoeff * forceOnRearWheel;
        
        latForceFront = Mathf.Clamp(latForceFront, -maxLatForceFront, maxLatForceFront);
        latForceRear = Mathf.Clamp(latForceRear, -maxLatForceRear, maxLatForceRear);
        
        float engineTurnOverRate = longVel * 60 * torqueMultiplier / (2 * Mathf.PI * wheelRadius);
        
        engineTurnOverRate = Mathf.Clamp(engineTurnOverRate, minRPM, maxRPM);
        
        float engineTorque = rpmTorqueGraph.Evaluate(engineTurnOverRate);
        
        float actualTorque = engineTorque * torqueMultiplier * throttle;
        
        float tractionForce = actualTorque  / wheelRadius;
        
        Vector3 rollingResistance = -rollingResistanceCoeff * localVelocity;
        Vector3 dragResistance = -0.5f * dragCoeff * frontalArea * densityOfAir * localVelocity * localSpeed;
        
        Vector3 resistanceTotal = rollingResistance + dragResistance;
        
        Vector3 totalForce = Vector3.zero;
        
        totalForce.z = tractionForce + latForceFront * Mathf.Sin(steeringAngle * Mathf.Deg2Rad) + resistanceTotal.z;
        totalForce.x = latForceRear + latForceFront * Mathf.Cos(steeringAngle * Mathf.Deg2Rad) + resistanceTotal.x;
        
        
        
        Debug.DrawRay(transform.position, transform.forward * totalForce.z, Color.green);
        Debug.DrawRay(transform.position, transform.right * totalForce.x, Color.red);
        
        
        float totalTorque = Mathf.Cos(steeringAngle * Mathf.Deg2Rad) * latForceFront * frontOffsetFromCOM - latForceRear * rearOffsetFromCOM;
        
        rb.AddRelativeForce(totalForce);
        rb.AddTorque(totalTorque * transform.up);
        
        
        speedText.text = "Speed : \n" + localVelocity + " = " + localSpeed + "m/s" + "\n" + localSpeed * 18 / 5 + "km/hr";
        totalForceText.text = "Total Force : " + totalForce + " = " + totalForce.magnitude;
        driveForceText.text = "Traction Force : " + tractionForce;
        rollingResText.text = "Rolling Res : " + rollingResistance + " = " + rollingResistance.magnitude;
        dragText.text = "Drag : " + dragResistance + " = " + dragResistance.magnitude;
        torqueText.text = "Torque : " + localAngularSpeed;
        steerText.text = "Steer Angle : " + steeringAngle;
        
    }
}
