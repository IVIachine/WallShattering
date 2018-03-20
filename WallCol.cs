using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class IntIntDictionary : SerializableDictionary<int, int> { }

public class WallCol : MonoBehaviour
{
    [HideInInspector]
    [SerializeField]
    private string mAxis;

    [SerializeField]
    private IntIntDictionary IntegerIntegerStore = IntIntDictionary.New<IntIntDictionary>();
    private Dictionary<int, int> mPathLookup
    {
        get { return IntegerIntegerStore.dictionary; }
    }

    /// <summary>
    /// Invalidate shape at index to in a way destroy that triangle collider
    /// </summary>
    /// <param name="index">Triangle index to remove</param>
    public void updateColliderAtIndex(int index)
    {
        int actualIndex = mPathLookup[index];
        PolygonCollider2D col = GetComponent<PolygonCollider2D>();
        col.SetPath(actualIndex, new Vector2[3] { Vector2.zero, Vector2.zero, Vector2.zero });
    }

    /// <summary>
    /// Create the initial starting polygon collider. Take into account the axis it was modeled on
    /// </summary>
    /// <returns>Known axis</returns>
    public string generatePolygonCollider(Mesh meshRef)
    {
        mPathLookup.Clear();
        Mesh M = meshRef;

        if (GetComponent<MeshFilter>())
        {
            M = GetComponent<MeshFilter>().sharedMesh;
        }
        else if (GetComponent<SkinnedMeshRenderer>())
        {
            M = GetComponent<SkinnedMeshRenderer>().sharedMesh;
        }

        Material[] materials = new Material[0];
        if (GetComponent<MeshRenderer>())
        {
            materials = GetComponent<MeshRenderer>().sharedMaterials;
        }
        else if (GetComponent<SkinnedMeshRenderer>())
        {
            materials = GetComponent<SkinnedMeshRenderer>().sharedMaterials;
        }

        Vector3[] verts = M.vertices;
        PolygonCollider2D col = gameObject.GetComponent<PolygonCollider2D>();
        col.enabled = false;
        col.points = new Vector2[0];
        mPathLookup.Clear();

        for (int submesh = 0; submesh < M.subMeshCount; submesh++)
        {
            int[] indices = M.GetTriangles(submesh);

            for (int i = 0; i < indices.Length; i += 3)
            {
                Vector3[] newVerts = new Vector3[3];
                for (int n = 0; n < 3; n++)
                {
                    int index = indices[i + n];
                    newVerts[n] = verts[index];
                }

                Vector2[] actualPoints = new Vector2[(newVerts.Length)];
                for (int j = 0; j < newVerts.Length; j++)
                {
                    actualPoints[j] = new Vector2(newVerts[j].x, newVerts[j].z);
                }

                mPathLookup.Add(indices[i], col.pathCount - 1);
                col.SetPath(col.pathCount - 1, actualPoints);
                col.pathCount++;
            }
        }

        if (col.shapeCount <= 0)
        {
            col.points = new Vector2[0];
            mPathLookup.Clear();

            for (int submesh = 0; submesh < M.subMeshCount; submesh++)
            {
                int[] indices = M.GetTriangles(submesh);

                for (int i = 0; i < indices.Length; i += 3)
                {
                    Vector3[] newVerts = new Vector3[3];
                    for (int n = 0; n < 3; n++)
                    {
                        int index = indices[i + n];
                        newVerts[n] = verts[index];
                    }

                    Vector2[] actualPoints = new Vector2[(newVerts.Length)];
                    for (int j = 0; j < newVerts.Length; j++)
                    {
                        actualPoints[j] = new Vector2(newVerts[j].x, newVerts[j].y);
                    }

                    mPathLookup.Add(indices[i], col.pathCount - 1);
                    col.SetPath(col.pathCount - 1, actualPoints);
                    col.pathCount++;
                }
            }
            mAxis = "xy";
            col.enabled = true;
            return "xy";
        }
        else
        {
            mAxis = "xz";
            col.enabled = true;
            return "xz";
        }
    }
}