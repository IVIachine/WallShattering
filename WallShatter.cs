using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

public class WallShatter : MonoBehaviour
{
    struct Explosion
    {
        public float time, radii;
        public Vector3 pos;
    };

    //simulation variables
    [SerializeField]
    private float baseSpeed;

    [SerializeField]
    private float speedFromDistance;

    [SerializeField]
    private float speedRand;

    [SerializeField]
    private float baseRotationSpeed;

    [SerializeField]
    private float rotationSpeedFromDist;

    [SerializeField]
    private float rotSpeedRand;

    [SerializeField]
    private float removalTime;

    [SerializeField]
    private float mExplosionStrengthModifier = 1.0f;

    [SerializeField]
    private bool mRemoveTriangles;

    [SerializeField]
    private float[] explosionTimes;

    [SerializeField]
    private float[] explosionRadii;

    [SerializeField]
    private Vector3[] explosionPositions;

    [SerializeField]
    private ComputeShader mSimulationShader;

    //compute buffers
    private ComputeBuffer mVertices;
    private ComputeBuffer mOldVertices;
    private ComputeBuffer mIndices;

    private ComputeBuffer mExplosionTimes;
    private ComputeBuffer mExplosionRadii;
    private ComputeBuffer mExplosionLocations;

    private int mExplosionMax = 150;
    private int mCurrentExplosions;
    private int mWallIndex;

    private bool mExpectingCallback;
    private bool mHasExploded;

    private Bounds mOriginalBounds;
    private int[] mKernals;

    private List<int> mTempIndices;

    /// <summary>
    /// Remove shared vertices and generate the collider based on the triangles
    /// </summary>
    public void initializeData()
    {
#if UNITY_EDITOR
        //Mesh newMesh = Mesh.Instantiate(avoidVertexSharing());
        //AssetDatabase.CreateAsset(newMesh, "Assets/Scenes/scene1/models/walls/" + newMesh.name + gameObject.name + ".asset");
        //AssetDatabase.SaveAssets();
        //GetComponent<MeshFilter>().mesh = newMesh;
        //GetComponent<WallCol>().generatePolygonCollider(GetComponent<MeshFilter>().sharedMesh);
        mSimulationShader = ComputeShader.Instantiate(mSimulationShader);
        EditorUtility.SetDirty(this);
#endif
    }

    public void InitWall(int index)
    {
        mKernals = new int[4];
        mKernals[0] = mSimulationShader.FindKernel("SimulationHigh");
        mKernals[1] = mSimulationShader.FindKernel("SimulationMedium");
        mKernals[2] = mSimulationShader.FindKernel("SimulationLow");
        mKernals[3] = mSimulationShader.FindKernel("SimulationLowest");

        explosionTimes = new float[mExplosionMax];
        explosionRadii = new float[mExplosionMax];
        explosionPositions = new Vector3[mExplosionMax];
;
        mOriginalBounds = GetComponent<MeshFilter>().mesh.bounds;
        mWallIndex = index;

        mTempIndices = new List<int>();
        mCurrentExplosions = 0;
        mExpectingCallback = false;

        //original data buffers
        Vector3[] pOldVertexInitBuffer = GetComponent<MeshFilter>().mesh.vertices;
        mOldVertices = new ComputeBuffer(GetComponent<MeshFilter>().mesh.vertexCount, System.Runtime.InteropServices.Marshal.SizeOf(Vector3.zero), ComputeBufferType.Default);
        mOldVertices.SetData(pOldVertexInitBuffer);

        Vector3[] pVertexInitBuffer = GetComponent<MeshFilter>().mesh.vertices;
        mVertices = new ComputeBuffer(GetComponent<MeshFilter>().mesh.vertexCount, System.Runtime.InteropServices.Marshal.SizeOf(Vector3.zero), ComputeBufferType.Default);
        mVertices.SetData(pVertexInitBuffer);

        int tmp = 0;
        int[] pIndexInitBuffer = GetComponent<MeshFilter>().mesh.triangles;
        mIndices = new ComputeBuffer(GetComponent<MeshFilter>().mesh.triangles.Length, System.Runtime.InteropServices.Marshal.SizeOf(tmp), ComputeBufferType.Default);
        mIndices.SetData(pIndexInitBuffer);

        //explosion buffers
        Vector3[] pExplPosInitBuffer = new Vector3[mExplosionMax];
        mExplosionLocations = new ComputeBuffer(mExplosionMax, System.Runtime.InteropServices.Marshal.SizeOf(Vector3.zero), ComputeBufferType.Default);
        mExplosionLocations.SetData(pExplPosInitBuffer);

        float tmp1 = 0;
        float[] pExplTimeInitBuffer = new float[mExplosionMax];
        for (int i = 0; i < mExplosionMax; i++)
        {
            pExplTimeInitBuffer[i] = -1.0f;
        }
        mExplosionTimes = new ComputeBuffer(mExplosionMax, System.Runtime.InteropServices.Marshal.SizeOf(tmp1), ComputeBufferType.Default);
        mExplosionTimes.SetData(pExplTimeInitBuffer);

        float[] pExplRadiiInitBuffer = new float[mExplosionMax];
        mExplosionRadii = new ComputeBuffer(mExplosionMax, System.Runtime.InteropServices.Marshal.SizeOf(tmp1), ComputeBufferType.Default);
        mExplosionRadii.SetData(pExplRadiiInitBuffer);

        //set shader data
        mSimulationShader.SetFloat("baseSpeed", baseSpeed);
        mSimulationShader.SetFloat("speedFromDistance", speedFromDistance);
        mSimulationShader.SetFloat("speedRand", speedRand);

        mSimulationShader.SetFloat("baseRotationSpeed", baseRotationSpeed);
        mSimulationShader.SetFloat("rotationSpeedFromDist", rotationSpeedFromDist);
        mSimulationShader.SetFloat("rotSpeedRand", rotSpeedRand);

        mSimulationShader.SetFloat("removalTime", removalTime);
        mSimulationShader.SetInt("objectIndex", mWallIndex);

        foreach (int id in mKernals)
        {
            mSimulationShader.SetBuffer(id, "vertexPositions", mVertices);
            mSimulationShader.SetBuffer(id, "triangleIndices", mIndices);
            mSimulationShader.SetBuffer(id, "oldVertexPositions", mOldVertices);

            mSimulationShader.SetBuffer(id, "explosionTimes", mExplosionTimes);
            mSimulationShader.SetBuffer(id, "explosionLocations", mExplosionLocations);
            mSimulationShader.SetBuffer(id, "explosionRadii", mExplosionRadii);
        }

        GetComponent<MeshRenderer>().material.SetBuffer("vertexPositions", mVertices);
        GetComponent<MeshRenderer>().material.SetInt("playing", 1);
        mHasExploded = false;
    }

    public void SetManagerBuffers(ComputeBuffer colIndex, ComputeBuffer triIndex, 
        ComputeBuffer colObj, ComputeBuffer triObj)
    {
        foreach (int id in mKernals)
        {
            mSimulationShader.SetBuffer(id, "colliderRemovalsObjects", colObj);
            mSimulationShader.SetBuffer(id, "triangleRemovalsObjects", triObj);

            mSimulationShader.SetBuffer(id, "colliderRemovalsIndices", colIndex);
            mSimulationShader.SetBuffer(id, "triangleRemovalsIndices", triIndex);
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (mCurrentExplosions > 0)
        {
            mSimulationShader.SetInt("numExplosions", mCurrentExplosions);
            mTempIndices.Clear();

            for (int i = 0; i < mCurrentExplosions; i++)
            {
                if (explosionTimes[i] != 0)
                    explosionTimes[i] += Time.deltaTime;
                else
                    mTempIndices.Add(i);
            }

            mExplosionTimes.SetData(explosionTimes);

            if(!mHasExploded)
            {
                GetComponent<MeshFilter>().mesh.bounds = new Bounds(Vector3.zero, Vector3.one * float.MaxValue);
            }

            mHasExploded = true;

            int kernelId = 0;
            uint actual_xGroups = 0;
            foreach (int id in mKernals)
            {
                uint xGroups = 0;
                uint yGroups = 0;
                uint zGroups = 0;
                mSimulationShader.GetKernelThreadGroupSizes(id, out xGroups, out yGroups, out zGroups);
                if ((float)GetComponent<MeshFilter>().mesh.triangles.Length / (float)3 % (float)xGroups == 0)
                {
                    kernelId = id;
                    actual_xGroups = xGroups;
                    break;
                }
            }

            mSimulationShader.Dispatch(kernelId, GetComponent<MeshFilter>().mesh.triangles.Length / 3 / (int)actual_xGroups, 1, 1);

            foreach (int index in mTempIndices)
            {
                explosionTimes[index] += Time.deltaTime;
            }

            if (mTempIndices.Count > 0)
                mExplosionTimes.SetData(explosionTimes);
        }
        else
        {
            if (mHasExploded)
                GetComponent<MeshFilter>().mesh.bounds = mOriginalBounds;

            mHasExploded = false;
        }
    }

    public void RemoveTriangleAtIndex(int index, ref List<int> workingTriangles)
    {
        workingTriangles.RemoveAt(index * 3 + 2);
        workingTriangles.RemoveAt(index * 3 + 1);
        workingTriangles.RemoveAt(index * 3);
    }

    public void SetIndexData()
    {
        mIndices.SetData(GetComponent<MeshFilter>().mesh.triangles);
    }

    public void RemoveColliderAtIndex(int index)
    {
        GetComponent<WallCol>().updateColliderAtIndex(index);
    }

    /// <summary>
    /// Check to see if any explosions are outdated
    /// Sort explosion buffers after removed
    /// </summary>
    private void CheckForRemovals()
    {
        if (mCurrentExplosions <= 0)
            return;

        //Remove old explosions
        int decrementAmount = 0;

        for (int i = 0; i < mCurrentExplosions; i++)
        {
            if (explosionTimes[i] > removalTime)
            {
                explosionTimes[i] = -1.0f;
                decrementAmount++;
                Camera.main.GetComponent<WallManager>().RemoveExplosion();
            }
        }

        //if any explosions have timed out
        if (decrementAmount > 0)
        {
            //store in temporary to prevent multiple sort calls
            Explosion[] explosions = new Explosion[mExplosionMax];
            for (int i = 0; i < mExplosionMax; i++)
            {
                explosions[i].pos = explosionPositions[i];
                explosions[i].radii = explosionRadii[i];
                explosions[i].time = explosionTimes[i];
            }

            //sort based on times
            Array.Sort(explosionTimes, explosions);
            Array.Reverse(explosions);

            //store back in arrays
            for (int i = 0; i < mExplosionMax; i++)
            {
                explosionPositions[i] = explosions[i].pos;
                explosionRadii[i] = explosions[i].radii;
                explosionTimes[i] = explosions[i].time;
            }

            //set compute buffers
            mExplosionTimes.SetData(explosionTimes);
            mExplosionRadii.SetData(explosionRadii);
            mExplosionLocations.SetData(explosionPositions);
        }

        mCurrentExplosions -= decrementAmount;
    }

    /// <summary>
    /// Add a new explosion at the target location
    /// </summary>
    /// <param name="pos">Explosion location</param>
    /// <param name="strength">Explosion strength</param>
    /// <param name="dir">Explosion direction</param>
    /// <param name="particles">Should use particles</param>
    public void AddNewExplosion(Vector3 pos, float strength, Vector2 dir, bool particles)
    {
        if (mCurrentExplosions >= mExplosionMax)
            return;

        bool assigned = false;
        for (int i = 0; i < mCurrentExplosions; i++)
        {
            if (explosionTimes[i] == -1.0f)
            {
                explosionPositions[i] = transform.InverseTransformPoint(new Vector3(pos.x, pos.y, transform.position.z));
                explosionTimes[i] = 0;
                explosionRadii[i] = strength / transform.localScale.x * mExplosionStrengthModifier;
                assigned = true;
                break;
            }
        }

        //if no spaces were found in explosion array
        if (!assigned)
        {
            explosionPositions[mCurrentExplosions] = transform.InverseTransformPoint(new Vector3(pos.x, pos.y, transform.position.z));
            explosionTimes[mCurrentExplosions] = 0;
            explosionRadii[mCurrentExplosions] = strength / transform.localScale.x * mExplosionStrengthModifier;
        }

        mCurrentExplosions++;
        mExplosionTimes.SetData(explosionTimes);
        mExplosionRadii.SetData(explosionRadii);
        mExplosionLocations.SetData(explosionPositions);

        if (particles)
        {
            Quaternion rot = Quaternion.LookRotation(Vector3.forward, -dir);
            GameObject newSystem = GameObject.Instantiate(Resources.Load("explosionParticles") as GameObject, new Vector3(pos.x, pos.y,
                -10), rot);
            newSystem.transform.localScale = newSystem.transform.localScale * (strength * 25);
        }
        graphGeneratorScript.sGraphGen.updateGraph(pos, strength);

        mExpectingCallback = true;
        Camera.main.GetComponent<WallManager>().AddExplosion();
        Camera.main.GetComponent<WallManager>().AddColRemovalExpectation();
    }

    public void CustomPostRender()
    {
        CheckForRemovals();
    }

    /// <summary>
    /// Create mesh preventing triangles from sharing vertices
    /// </summary>
    /// <returns></returns>
    private Mesh AvoidVertexSharing()
    {
        Mesh mesh = GetComponent<MeshFilter>().sharedMesh;

        // unshare verts
        int subMeshCnt = mesh.subMeshCount;
        int[] tris = mesh.triangles;
        int triCnt = mesh.triangles.Length;
        int[] newTris = new int[triCnt];
        Vector3[] sourceVerts = mesh.vertices;
        Vector3[] sourceNorms = mesh.normals;
        Vector2[] sourceUVs = mesh.uv;

        Vector3[] newVertices = new Vector3[triCnt];
        Vector3[] newNorms = new Vector3[triCnt];
        Vector2[] newUVs = new Vector2[triCnt];

        int offsetVal = 0;
        for (int k = 0; k < subMeshCnt; k++)
        {
            int[] sourceIndices = mesh.GetTriangles(k);

            int[] newIndices = new int[sourceIndices.Length];

            // create a unique vertex for every index in the original mesh
            for (int i = 0; i < sourceIndices.Length; i++)
            {
                int newIndex = sourceIndices[i];
                int iOffset = i + offsetVal;
                newIndices[i] = iOffset;
                newVertices[iOffset] = sourceVerts[newIndex];
                newNorms[iOffset] = sourceNorms[newIndex];
                newUVs[iOffset] = sourceUVs[newIndex];
            }
            offsetVal += sourceIndices.Length;

            mesh.vertices = newVertices;
            mesh.normals = newNorms;
            mesh.uv = newUVs;

            mesh.SetTriangles(newIndices, k);
        }

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private void OnDestroy()
    {
        mVertices.Dispose();
        mOldVertices.Dispose();
        mIndices.Dispose();

        mExplosionTimes.Dispose();
        mExplosionRadii.Dispose();
        mExplosionLocations.Dispose();
    }
}
