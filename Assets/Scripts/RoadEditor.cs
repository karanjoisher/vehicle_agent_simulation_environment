using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//TODO(Suhaib): Figure out the bug regaurding the direction of the texture

public class RoadEditor : MonoBehaviour {

    public int lane_num = 1;
    public bool both_sides = true;

   
    public float lane_width = 3.6f; // width of each lane is 3.6m


    //TODO(Suhaib): Make tiling automatic
    public float tile = 1f;

    public bool edit_mode = false;
    private List<Vector3> points;

    
    public void Start()
    {
        //DebugInfo();
    }

    public void DebugInfo()
    {
        Debug.Log("************ROAD**************");

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


    public void ToggleEditMode()
    {
        edit_mode = !edit_mode;

        //Debug.Log("Toggle");

    }

    public bool EditMode()
    {
        return edit_mode;
    }

    private Vector3 Vector3XZ(Vector2 vec)
    {
        return new Vector3(vec.x, 0f, vec.y);
    }

    public float AngleBetween(Vector2 vec1, Vector2 vec2)
    {
        float sign = Mathf.Sign(Vector3.Cross(Vector3XZ(vec1), Vector3XZ(vec2)).y);

        return -sign * Mathf.Acos(Vector2.Dot(vec1, vec2) / (vec1.magnitude * vec2.magnitude)) * Mathf.Rad2Deg;
    }

    private Vector2 Vector2XZ(Vector3 vec)
    {
        return new Vector2(vec.x, vec.z);
    }

    public void ReCalcMesh()
    {
        float half_width = lane_num * lane_width * (both_sides ? 1 : 0.5f);

        if (gameObject.GetComponent<MeshFilter>().sharedMesh != null)
        {
            gameObject.GetComponent<MeshFilter>().sharedMesh.Clear();
        }

        if (points.Count >= 2)
        {
            Mesh mesh = new Mesh();

            Vector3[] vertices = new Vector3[points.Count * 2];
            Vector2[] uvs = new Vector2[points.Count * 2];
            int[] tris = new int[(points.Count - 1) * 6];


            Vector3 sum = Vector3.zero;

            for (int i = 0; i < points.Count; i++)
            {
                sum += points[i];
            }

            Vector3 avg = sum / points.Count;

            gameObject.transform.position = avg;

            Vector2[] uv_coods = { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f * tile), new Vector2(1f, 1f * tile) };

            float angle = 0;
            Vector3 mid_point;

            angle = Mathf.Deg2Rad * (AngleBetween(-Vector2.left, Vector2XZ(points[1] - points[0])) - 90);

            mid_point = points[0] - avg;

            vertices[0 * 2 + 0] = mid_point + new Vector3(half_width * Mathf.Cos(angle), 0f, half_width * Mathf.Sin(angle));
            vertices[0 * 2 + 1] = mid_point - new Vector3(half_width * Mathf.Cos(angle), 0f, half_width * Mathf.Sin(angle));

            uvs[0 * 2 + 0] = uv_coods[(0 * 2 + 0) % 4];
            uvs[0 * 2 + 1] = uv_coods[(0 * 2 + 1) % 4];

            for (int i = 1; i < points.Count - 1; i++)
            {
                Vector2 vec1 = Vector2XZ(points[i + 1] - points[i]).normalized;
                Vector2 vec2 = Vector2XZ(points[i - 1] - points[i]).normalized;

                angle = AngleBetween(vec2, vec1) * Mathf.Deg2Rad / 2;

                Vector2 middle = ((vec1 + vec2) / 2f).normalized;

                Debug.Log(angle * Mathf.Rad2Deg * 2);

                mid_point = points[i] - avg;

                float mag = half_width / Mathf.Sin(angle);

            

                vertices[i * 2 + 0] = mid_point + mag * Vector3XZ(middle);
                vertices[i * 2 + 1] = mid_point - mag * Vector3XZ(middle);

                uvs[i * 2 + 0] = uv_coods[(i * 2 + 0) % 4];
                uvs[i * 2 + 1] = uv_coods[(i * 2 + 1) % 4];


            }

            int last = points.Count - 1;

            angle = Mathf.Deg2Rad * (AngleBetween(-Vector2.left, Vector2XZ(points[last] - points[last - 1])) - 90);

            mid_point = points[last] - avg;

            vertices[last * 2 + 0] = mid_point + new Vector3(half_width * Mathf.Cos(angle), 0f, half_width * Mathf.Sin(angle));
            vertices[last * 2 + 1] = mid_point - new Vector3(half_width * Mathf.Cos(angle), 0f, half_width * Mathf.Sin(angle));

            uvs[last * 2 + 0] = uv_coods[(last * 2 + 0) % 4];
            uvs[last * 2 + 1] = uv_coods[(last * 2 + 1) % 4];


            int q_index = 0;

            for (int i = 0; i < tris.Length; i += 6)
            {


                tris[i + 0] = q_index + 0;
                tris[i + 1] = q_index + 1;
                tris[i + 2] = q_index + 2;
                tris[i + 3] = q_index + 2;
                tris[i + 4] = q_index + 1;
                tris[i + 5] = q_index + 3;

                q_index += 2;

            }

            

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = tris;

            mesh.RecalculateNormals();

            gameObject.GetComponent<MeshFilter>().sharedMesh = mesh;

        }
    }

    public void AddRoadPoint(Vector3 point)
    {
        if (points == null)
        {
            points = new List<Vector3>();

            gameObject.AddComponent<MeshFilter>();
            gameObject.AddComponent<MeshRenderer>();
        }

        points.Add(point);

        ReCalcMesh();
        

    }

    public void RemoveLastPoint()
    {

        if (points.Count <= 0) return;

        points.RemoveAt(points.Count - 1);

        ReCalcMesh();

    }

   
   


}
