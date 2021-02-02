//
// IPC Information
// So here we have 2 things that are beings transferred between python and Unity
// pythonData is the things that python will be sending me
// its format is as follows
// pythonData[0] = predicted veloicty in m/s by python 
// pythonData[1] = predicted steering angle ranging from most left (0) to most right(255)
//
// carControlData is the thing Unity is sending python 
// the same format as pythonData follows here
// carControlData[0] = actual veloicty in m/s by python 
// carControlData[1] = actual steering angle ranging from most left (0) to most right(255)
//
// This send to python these two information
// python model then send the predicted version of the same information back
// unity then adjusts the actual values to match the predicted values as best it could
// 
// Note there is a possible overflow with velocity that we arent handling yet and I don't think byte has enough granularity, so mostly we will have to change it to float
// this would require changing the IPCController but I don't want to break anything so not gonna touch it
//




using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class Car : MonoBehaviour
{
    public List<AxleInfo> axleInfos; // the information about each individual axle
    public float maxMotorTorque; // maximum torque the motor can apply to wheel
    public float maxSteeringAngle; // maximum steer angle the wheel can have
    public float brake_torque;
    
    public float drag_coeff;
    public float air_density;
    public float frontal_area;
    
    public bool USE_CONTROLLER;
    
    public Transform com;
    
    public Text ui_text;
    
    private float prev_vel;
    private float curr_vel;
    float left_distance = -1f; 
    float right_distance = -1f;
	private float prev_dist_from_cen;
	private float reward;
	private float total_reward; 
	private bool train = false; 
	private int counter = 0; 

    private bool in_collision = false;

    private float speed_limit = 25.0f;  //(Omkar): Not used for the time being. Will be used if magic throttling is used. 
    private float break_constant = 100.0f;  //(Omkar): To decide how strongly the car should brake 

    private Rigidbody rb;
    
    private int boundary_layer_mask = 1 << 8;
    int collisions = 0;
    float distance = 0.0f;
	private float road_width = 8.0f; 
    
    // NOTE(KARAN) : Adding these for ipc testing
    public float[] carControlsData; 
    public float[] pythonData;
	
	private Vector3 start_position;
	private  Quaternion start_rotation;

    private Vector3 last_position;
	const int locations = 7; 
	private float[, ] spawn_locations = new float[locations, 3] {{40.4f, -0.935f, 100.4f}, {40.4f, -0.935f, 276.8f}, {105.73f, -0.935f, 345.78f}, {138.11f, -0.935f, 297.74f}, {189.686f, -0.935f, 291.15f}, {206.511f, -0.935f, 167.80f}, {137.1469f, -0.935f, 73.1245f}}; 
	
	private float[, ] spawn_rotations = new float[locations, 3] {{0.001f, 0f, 0f}, {0.001f, 0f, 0f}, {0.001f, 111.55f, 0f}, {-0.001f, 125.875f, 0f}, {0.099f,  89.23601f, 0f}, {0.012f, -148.737f, -0.007f}, {-0.002f, -92.037f, -0.001f}};
	

    
    public static float RemapRange(float val, float min, float max, float newMin, float newMax)
    {
        float result;
        
        result = ((val / (max - min)) * (newMax - newMin)) + newMin;
        
        return result;
    }
    
    public void Start()
    {
        rb = GetComponent<Rigidbody>();
		
		start_position= transform.position;
		start_rotation = transform.rotation;

        last_position = transform.position;

        prev_vel = rb.velocity.magnitude;
        
        
        // START(KARAN) : Adding this for ipc testing
        //carControlsData = new byte[4];
        carControlsData = new float[6];
        //Debug.Log(sizeof(carControlsData[0]));
        pythonData = new float[3];
        // END(KARAN)


		
		prev_dist_from_cen = 0.0f;
    }
    
    public void Update()
    {
        //NOTE(KARAN) : Uploading car control data
        //(Omkar): Don't know what to pass, so passing more than required attributes: Speed, HA, Left_dist, Right_Dist, VA, Angular Velocity 
        carControlsData[0] = rb.velocity.magnitude;
        carControlsData[1] = Input.GetAxis("Horizontal");
		carControlsData[2] = left_distance;
		carControlsData[3] = right_distance;
        carControlsData[4] = reward; 
        carControlsData[5] = rb.angularVelocity.magnitude; 
		
		if (pythonData[2] > 0f)
		{
			//transform.position = start_position;
			//transform.rotation = start_rotation;

			int loc = Random.Range(0, locations); 
			transform.position = new Vector3(spawn_locations[loc, 0], spawn_locations[loc, 1], spawn_locations[loc, 2]); 
			transform.rotation = Quaternion.Euler(spawn_rotations[loc, 0], spawn_rotations[loc, 1], spawn_rotations[loc, 2]); 

			
			
			rb.velocity = new Vector3(0f, 0f, 0f);
			
		}
		

    }

    public void FixedUpdate()
    {
        RaycastHit ray_cast;

        left_distance = -1f; right_distance = -1f;
		
		
		// Use this for DrivingTest.unity 
        if (Physics.Raycast(transform.position, transform.right, out ray_cast, Mathf.Infinity, boundary_layer_mask))
        {
            right_distance = ray_cast.distance;
        }
        if (Physics.Raycast(transform.position, -transform.right, out ray_cast, Mathf.Infinity, boundary_layer_mask))
        {
            left_distance = ray_cast.distance;
        }
		
		//Debug.Log("Left: " + left_distance + " Right: " + right_distance); 
		//counter++; 
		//Debug.Log("Count: " + counter);
		
		/* OLD ONE: Use this for DrivingTest2.unity 
		if (Physics.Raycast(transform.position, transform.right, out ray_cast, Mathf.Infinity, boundary_layer_mask))
        {
            right_distance = ray_cast.distance;
        }
        if (Physics.Raycast(transform.position, -transform.right, out ray_cast, Mathf.Infinity, boundary_layer_mask))
        {
            left_distance = ray_cast.distance;
        }
		*/


        if (!in_collision && (left_distance < 1.0f || right_distance < 1.0f))
        {
            collisions++;
            in_collision = true;
        }

        if (left_distance >= 1.0f && right_distance >= 1.0f)
        {
            in_collision = false;
        }

        if (!in_collision)
        {
            distance += Vector3.Distance(transform.position, last_position);

        }
		/*
		float distance_from_center = -1;
		float half_lane_width = (road_width) / 2f;
		
		if (left_distance != -1f && right_distance != -1)
		{
			distance_from_center = half_lane_width - (left_distance < right_distance ? left_distance : right_distance);
		}
		else if (left_distance != -1f)
		{
			distance_from_center = half_lane_width + left_distance;
		}
		else if (right_distance != -1f)
		{
			distance_from_center = half_lane_width + right_distance;
		}
		else
		{
				//Debug.Log("Unhandled Case");
				distance_from_center = 5; // Make this an undesirable state 
		}
*/
		//reward = prev_dist_from_cen - Mathf.Abs(distance_from_center);
		
		if(left_distance < 0.95 && left_distance != -1f || right_distance < 2.5 && right_distance != -1f)
		{
			reward = -5;
			//Debug.Log("REDUCE");
		}
		else if(left_distance > road_width || right_distance > road_width)
		{
			reward = -100; 
		}
		else
		{
			reward = 1; 
		}
		
		//reward = -Mathf.Abs(distance_from_center); 
		
		//prev_dist_from_cen = Mathf.Abs(distance_from_center);
		
	    //Debug.Log("Reward obtained: " + reward);
		total_reward = total_reward + reward; 
		//Debug.Log("Total Reward: " + total_reward); 
		//Debug.Log("Collisions: " +  collisions); 
		
        last_position = transform.position;

 
        
        // NOTE_TO(Karan): left_distance and right_distance are now set you can send to python from here on out
        
        curr_vel = rb.velocity.magnitude;
        
        
        float acceleration = (curr_vel - prev_vel) / Time.deltaTime;
        
        prev_vel = curr_vel;
        
        ui_text.text = "Velocity     = " + Mathf.RoundToInt(rb.velocity.magnitude) + "m/s  = " + Mathf.RoundToInt(rb.velocity.magnitude * 18f / 5f) + "km/h\n";
        ui_text.text += "Acceleration = " + Mathf.RoundToInt(acceleration) + "m/s^2  = " + Mathf.RoundToInt(acceleration * 18 * 18f / 5f / 5f) + "km/h^2\n";
        float air_res_force = 0.5f * drag_coeff * frontal_area * air_density * rb.velocity.sqrMagnitude;
        
        if (rb.velocity.magnitude != 0f)
        {
            rb.AddForce(air_res_force * rb.velocity * (-1f / rb.velocity.magnitude));
        }
        
        //Debug.Log(rb.centerOfMass);
        
        float verticalAxis = 0.0f;
        float horizontalAxis = 0.0f;
        
        float motor = 0;
        float steering = 0;

        //(Omkar): Break Torque will start at 0 and will be non-zero if car is moving forward and down button is pressed. 
        float brake_torque = 0;
        var actual_velocity = rb.velocity;
        var car_direction = transform.InverseTransformDirection(actual_velocity);
        float speed = actual_velocity.magnitude;

        // NOTE(KARAN): If in IPC mode, read the data that was sent by python via IPC
        if (IPCController.singleton.on)
        {
            
            // NOTE(KARAN): 0 = full left/brake; 255 = full right/throttle
            float desired_velocity = pythonData[0];
            float desired_steering = pythonData[1];// RemapRange(pythonData[1], 0.0f, 255.0f, -1.0f, 1.0f);
			
			if (train){
				 if (right_distance < 2.5f)
				 {
					 desired_steering = -0.6f;
				 }
				else if(left_distance < 0.95f)
				{
					desired_steering = 0.6f; 
				}
				
			}
            
            horizontalAxis = desired_steering;
            verticalAxis = desired_velocity;
            
            /*Debug.Log("horizontalAxis: " + pythonData[0] + ", verticalAxis: " + pythonData[1]);
            
            horizontalAxis = desired_steering; // assuming instantaneous steering turn
            */
            //if (desired_velocity < curr_vel) verticalAxis = -1.0f;
            //else if (desired_velocity > curr_vel) verticalAxis = -1.0f;
            //else verticalAxis = 0.0f;
            
            
        }
        else
        {
            if (USE_CONTROLLER)
            {
                //TODO(Suhaib): find a way to use the trigger for accelerating and brake
                verticalAxis = Input.GetAxis("Fire1");
                horizontalAxis = Input.GetAxis("Horizontal");
            }
            else
            {
                verticalAxis = Input.GetAxis("Vertical");
                horizontalAxis = Input.GetAxis("Horizontal");
            }
            
            
            
        }

        //(Omkar): Apply break torque if car is moving forward only (along z axis). 
        if (verticalAxis < 0f && car_direction.z > 0)
        {
            brake_torque = break_constant * verticalAxis;
        }

        //(Omkar): Magic throttling 
        //speed = rb.velocity.magnitude; 
        //motor = maxMotorTorque * (1.0f - (horizontalAxis*horizontalAxis) - ((speed/speed_limit)*(speed/speed_limit))) ; 


        motor = maxMotorTorque * verticalAxis;  //maxMotorTorque * Input.GetAxis("Vertical");
        steering = maxSteeringAngle * horizontalAxis; //maxSteeringAngle * Input.GetAxis("Horizontal");
		//Debug.Log("For HA: " + horizontalAxis + " Steering Angle = " + steering); 
		Debug.Log("For VA: " + verticalAxis + " Throttle = " + motor); 
        
        ui_text.text += "Steering Angle = " + Mathf.RoundToInt(steering) + "  Throttle : " + (motor < 0 ? 0 : Mathf.RoundToInt(motor)) + "\n";
        
        ui_text.text += "Braking : " + (motor < 0 ? "(*)" : "( )");
        
        
        
        foreach (AxleInfo axleInfo in axleInfos)
        {
            /*
            WheelHit hit;
            if (axleInfo.leftWheel.GetGroundHit(out hit))
            {
                Debug.Log("Forward Slip : " + hit.forwardSlip + ", Sideways Slip : " + hit.sidewaysSlip);
            }
            
            
            if (axleInfo.rightWheel.GetGroundHit(out hit))
            {
                Debug.Log("Forward Slip : " + hit.forwardSlip + ", Sideways Slip : " + hit.sidewaysSlip);
                
            }
            */
            
            if (axleInfo.steering)
            {
                axleInfo.leftWheel.steerAngle = steering;
                axleInfo.rightWheel.steerAngle = steering;
            }
            if (axleInfo.motor)
            {
                
                axleInfo.leftWheel.brakeTorque = brake_torque;
                axleInfo.rightWheel.brakeTorque = brake_torque;
                axleInfo.leftWheel.motorTorque = motor;
                axleInfo.rightWheel.motorTorque = motor;
                
            }
        }
    }
}

[System.Serializable]
public class AxleInfo
{
    public WheelCollider leftWheel;
    public WheelCollider rightWheel;
    public bool motor; // is this wheel attached to motor?
    public bool steering; // does this wheel apply steer angle?
}
