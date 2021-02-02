using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;

public class PedestrianDemo : MonoBehaviour {
    
	// Use this for initialization
    public bool on = false;
    GameObject agentTemplate;
	public float radius = 15.0f;
    public int numTries = 30;
    public int numRegions = 8;
    public int numSeedsPerRegion = 1;
    public int desiredPoints = 50;
    public float waitSeconds = 3.0f;
    
    float clock = 0.0f;
    bool firstTime = true;
    List<Vector3> points;
    GameObject []agents;
    Vector3[] destinations;
    //public float displayRadius = 1.0f;
    void Start () 
    {
		
	}
	
	// Update is called once per frame
    void UpdateNPCs()
    {
        for(int i = 0; i < agents.Length; i++)
        {
            if((agents[i].transform.position - destinations[i]).sqrMagnitude <= (5.0f * 5.0f))
            {
                destinations[i] = points[Random.Range(0, points.Count)];
                agents[i].GetComponent<NavMeshAgent>().destination = destinations[i];
            }
        }
    }
    
    
    
	void Update () 
    {
        if(Input.GetKeyDown(KeyCode.F3))
        {
            on = !on;
        }
        
        if(on)
        {
            if(firstTime)
            {
                // NOTE(KARAN): Moved the stuff that should happen at the start method here so that script can be turned on whenever you wish
                firstTime = false;
                points = GenerateRandomNavMeshPoints(radius, desiredPoints, "Walkable");
                agents = new GameObject[points.Count];
                destinations = new Vector3[points.Count];
                agentTemplate = GameObject.Find("PedestrianTemplate");
                for(int i = 0; i < points.Count; i++)
                {
                    agents[i] = (GameObject) Instantiate(agentTemplate, points[i], agentTemplate.transform.rotation);
                    agents[i].name = "Pedestrian" + i;
                    destinations[i] = points[Random.Range(0, points.Count)];
                    agents[i].GetComponent<NavMeshAgent>().destination = destinations[i];
                }
                Destroy(agentTemplate);
            }
            
            clock += Time.deltaTime;
            if(clock >= waitSeconds)
            {
                clock = 0.0f;
                UpdateNPCs();
            }
        }
    }
    bool IsIntersectingOtherPointsRadius(Vector3 point, List<Vector3> otherPoints, float radius)
    {
        bool result = false;
        
        for(int j = 0; j < otherPoints.Count; j++)
        {
            float squaredDistance = (otherPoints[j] - point).sqrMagnitude;
            if(squaredDistance < radius * radius)
            {
                result = true;
                break;
            }
        }
        
        return result;
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
        if((mask & (1 << NavMesh.GetAreaFromName("Road"))) != 0)
        {
            result = "Road";
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
    
    // TODO(KARAN): Allow multiple area masks
    List<Vector3> GenerateRandomNavMeshPoints(float radius, int desiredPoints, string areaName)
    {
        
        int areaMask = 1 << NavMesh.GetAreaFromName(areaName);
        List<Vector3> meshVerticies = new List<Vector3>();
        List<Vector3> seedPositions = new List<Vector3>();
        List<Vector3> generatedPoints = new List<Vector3>();
        
        NavMeshTriangulation navMeshInfo = NavMesh.CalculateTriangulation();
        
        for(int i = 0; i < navMeshInfo.areas.Length; i++)
        {
            int triangleAreaMask = 1 << navMeshInfo.areas[i];
            //Debug.Log(GetStringFromAreaMask(triangleAreaMask));
            if((triangleAreaMask & areaMask) != 0)
            {
                int triangleStartIndex = i * 3;
                for(int j = triangleStartIndex; j < triangleStartIndex + 3; j++)
                {
                    int vertexArrayIndex = navMeshInfo.indices[j];
                    meshVerticies.Add(navMeshInfo.vertices[vertexArrayIndex]);
                }
            }
        }
        
        int numMeshVerticies = meshVerticies.Count;
        int numVerticesPerRegion = (int)Mathf.Floor((float)numMeshVerticies/(float)numRegions);
        
        // HACK(KARAN): Hacky "Clustering", can use Kmeans if the result of this hack doesn't feel good
        for(int i = 0; i < numRegions; i++)
        {
            int rangeStart = i * numVerticesPerRegion;
            int rangeOnePastEnd = rangeStart + numVerticesPerRegion;
            
            // NOTE(KARAN): Select sample seeds from each region
            for(int j = 0; j < numSeedsPerRegion; j++)
            {
                int index = Random.Range(rangeStart, rangeOnePastEnd);
                
                Vector3 seedPosition = meshVerticies[index]; 
                seedPositions.Add(seedPosition);
                
                // If seed itself is respecting the radius criteria, add it
                if(!IsIntersectingOtherPointsRadius(seedPosition, generatedPoints, radius))
                {
                    generatedPoints.Add(seedPosition);
                }
            }
        }
        
        // NOTE(KARAN): If there are more seed points and the number of generated points is less than the desired num points
        while(generatedPoints.Count < desiredPoints && seedPositions.Count > 0)
        {
            // Randomly select a seed
            int seedIndex = Random.Range(0, seedPositions.Count);
            Vector3 position = seedPositions[seedIndex];
            
            // Try to generate a point that is atleast 'radius' units away from the seed point. 
            bool candidateAccepted = false;
            for(int i = 0; i < numTries; i++)
            {
                float angle = Random.value * Mathf.PI * 2;
                Vector3 dir = new Vector3(Mathf.Sin(angle), 0.0f, Mathf.Cos(angle));
                
                Vector3 navMeshSearchStart = position + (dir * Random.Range(radius, 2*radius));
                
                NavMeshHit hitInfo;
                Vector3 candidatePosition = new Vector3();
                
                if(NavMesh.SamplePosition(navMeshSearchStart, out hitInfo, 1.0f, areaMask))
                {
                    candidatePosition = hitInfo.position;
                    candidateAccepted = !IsIntersectingOtherPointsRadius(candidatePosition, generatedPoints, radius);
                }
                
                if(candidateAccepted)
                {
                    generatedPoints.Add(candidatePosition);
                    seedPositions.Add(candidatePosition);
                    break;
                }
            }
            
            if(!candidateAccepted)
            {
                seedPositions.RemoveAt(seedIndex);
            }
        }
        return generatedPoints;
    }
    
    /*
    void OnValidate()
    {
        if(on)
        {
            points = GenerateRandomNavMeshPoints(radius, desiredPoints, areaName);
            Debug.Log("Points: " + points.Count);
        }
    }
    
    
    void OnDrawGizmos()
    {
    
        foreach(Vector3 point in points)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(point, radius);
            
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(point, displayRadius);
        }
        
    }
    */
}
