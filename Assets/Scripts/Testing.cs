using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Testing : MonoBehaviour {

	// Use this for initialization
	void Start () {

        Debug.Log("************REF**************");

        Mesh mesh = gameObject.GetComponent<MeshFilter>().sharedMesh;

        Vector3[] verts = mesh.vertices;
        Vector2[] uvs = mesh.uv;
        int[] tris = mesh.triangles;

        Debug.Log("Vertices : ");

        foreach (Vector3 vertex in verts)
        {
            Debug.Log("\t" + vertex);
        }

        Debug.Log("UVs : ");

        foreach (Vector2 uv in uvs)
        {
            Debug.Log("\t" + uv);
        }

        Debug.Log("Indices : ");

        foreach (int tri in tris)
        {
            Debug.Log("\t" + tri);
        }


    }
	
	// Update is called once per frame
	void Update () {
		
	}
}
