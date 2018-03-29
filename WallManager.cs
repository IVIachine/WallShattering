using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class wallManager : MonoBehaviour
{
    private ComputeBuffer mColRemovalsIndices;
    private ComputeBuffer mColRemovalsObjects;

    private ComputeBuffer mTriangleRemovalsIndices;
    private ComputeBuffer mTriangleRemovalsObjects;

    //temp argument buffers
    private ComputeBuffer mArgBufferOne, mArgBufferTwo;

    private List<wallShatter> mWalls;

    private Dictionary<int, List<int>> mNoCopies;
    private List<int> mUniqueObjects;
    private int mNumTotalExplosions;
    private bool mExpectColRemovals;

    // Use this for initialization
    void Awake()
    {
        mExpectColRemovals = false;

        mNoCopies = new Dictionary<int, List<int>>();
        mUniqueObjects = new List<int>();

        //find all unique walls in the scene
        wallShatter[] shatters = GameObject.FindObjectsOfType<wallShatter>();
        mWalls = new List<wallShatter>(shatters);

        int index = 0;
        int totalTriangles = 0;
        foreach (wallShatter shatter in shatters)
        {
            shatter.InitWall(index);
            totalTriangles += shatter.gameObject.GetComponent<MeshFilter>().mesh.triangles.Length;
            index++;
        }

        //setup compute buffers
        int tmp = 0;
        int[] pColRemovalBuffer = new int[0];
        mColRemovalsObjects = new ComputeBuffer(totalTriangles, System.Runtime.InteropServices.Marshal.SizeOf(tmp), ComputeBufferType.Append);
        mColRemovalsObjects.SetData(pColRemovalBuffer);
        mColRemovalsObjects.SetCounterValue(0);

        int[] pTriangleRemovalBuffer = new int[0];
        mTriangleRemovalsObjects = new ComputeBuffer(totalTriangles, System.Runtime.InteropServices.Marshal.SizeOf(tmp), ComputeBufferType.Append);
        mTriangleRemovalsObjects.SetData(pTriangleRemovalBuffer);
        mTriangleRemovalsObjects.SetCounterValue(0);

        int[] pColRemovalBufferIndices = new int[0];
        mColRemovalsIndices = new ComputeBuffer(totalTriangles, System.Runtime.InteropServices.Marshal.SizeOf(tmp), ComputeBufferType.Append);
        mColRemovalsIndices.SetData(pColRemovalBufferIndices);
        mColRemovalsIndices.SetCounterValue(0);

        int[] pTriangleRemovalBufferIndices = new int[0];
        mTriangleRemovalsIndices = new ComputeBuffer(totalTriangles, System.Runtime.InteropServices.Marshal.SizeOf(tmp), ComputeBufferType.Append);
        mTriangleRemovalsIndices.SetData(pTriangleRemovalBufferIndices);
        mTriangleRemovalsIndices.SetCounterValue(0);

        mArgBufferOne = new ComputeBuffer(4, sizeof(int), ComputeBufferType.IndirectArguments);
        mArgBufferTwo = new ComputeBuffer(4, sizeof(int), ComputeBufferType.IndirectArguments);

        //set buffers
        foreach (wallShatter shatter in shatters)
        {
            shatter.gameObject.GetComponent<MeshRenderer>().material.SetBuffer("colliderRemovalsObj", mColRemovalsObjects);
            shatter.gameObject.GetComponent<MeshRenderer>().material.SetBuffer("triangleRemovalsObj", mTriangleRemovalsObjects);

            shatter.gameObject.GetComponent<MeshRenderer>().material.SetBuffer("colliderRemovalsIndices", mColRemovalsIndices);
            shatter.gameObject.GetComponent<MeshRenderer>().material.SetBuffer("triangleRemovalsIndices", mTriangleRemovalsIndices);
        }
    }

    private void OnDestroy()
    {
        mTriangleRemovalsIndices.Dispose();
        mTriangleRemovalsObjects.Dispose();

        mColRemovalsIndices.Dispose();
        mColRemovalsObjects.Dispose();

        mArgBufferOne.Dispose();
        mArgBufferTwo.Dispose();
    }

    /// <summary>
    /// Clear write targets
    /// </summary>
    private void OnPreRender()
    {
        Graphics.SetRandomWriteTarget(1, mTriangleRemovalsObjects, true);
        Graphics.SetRandomWriteTarget(2, mColRemovalsObjects, true);

        Graphics.SetRandomWriteTarget(3, mTriangleRemovalsIndices, true);
        Graphics.SetRandomWriteTarget(4, mColRemovalsIndices, true);
    }

    public void AddExplosion()
    {
        mNumTotalExplosions++;
    }

    public void RemoveExplosion()
    {
        mNumTotalExplosions--;
    }

    public void AddColRemovalExpectation()
    {
        mExpectColRemovals = true;
    }

    /// <summary>
    /// Check buffer lists, update walls if necessary
    /// </summary>
    private void OnPostRender()
    {
        if (mNumTotalExplosions <= 0)
            return;

        int[] args = new int[] { 0, 1, 0, 0 };

        Graphics.ClearRandomWriteTargets();

        //used to prevent copies so post render isn't called more than once on each wall
        List<wallShatter> uniqueShatters = new List<wallShatter>();

        //if an explosion happened this frame
        if (mExpectColRemovals)
        {
            ComputeBuffer.CopyCount(mColRemovalsIndices, mArgBufferOne, 0);
            mArgBufferOne.GetData(args);

            //get buffer count
            int collidersRemoved = args[0];

            //were any triangles hit
            if (collidersRemoved > 0)
            {
                //get append buffer data
                int[] indices2 = new int[collidersRemoved];
                mColRemovalsIndices.GetData(indices2);

                int[] objects2 = new int[collidersRemoved];
                mColRemovalsObjects.GetData(objects2);

                mUniqueObjects.Clear();

                //disable all colliders and organize list to unique objects
                foreach (int index in objects2)
                {
                    if (!mUniqueObjects.Contains(index))
                    {
                        if (!uniqueShatters.Contains(mWalls[index]))
                            uniqueShatters.Add(mWalls[index]);

                        mUniqueObjects.Add(index);
                        mWalls[index].GetComponent<PolygonCollider2D>().enabled = false;
                    }
                }

                //preallocate vertex array
                Vector3[] verts = new Vector3[10000];
                Vector3 maxLoc = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                for (int i = 0; i < indices2.Length; i++)
                {
                    mWalls[objects2[i]].RemoveColliderAtIndex(indices2[i]);
                    verts = mWalls[objects2[i]].GetComponent<MeshFilter>().mesh.vertices;

                    //Move vertices far away so projections aren't rendered on them
                    verts[indices2[i]] =
                         maxLoc;

                    verts[indices2[i] + 1] =
                        maxLoc;

                    verts[indices2[i] + 2] =
                        maxLoc;
                    
                    mWalls[objects2[i]].GetComponent<MeshFilter>().mesh.vertices = verts;
                }

                foreach (int index in mUniqueObjects)
                {
                    mWalls[index].GetComponent<PolygonCollider2D>().enabled = true;
                }

                //reset buffers
                mColRemovalsIndices.SetData(new int[0]);
                mColRemovalsIndices.SetCounterValue(0);
                mColRemovalsObjects.SetData(new int[0]);
                mColRemovalsObjects.SetCounterValue(0);
            }
        }

        ComputeBuffer.CopyCount(mTriangleRemovalsIndices, mArgBufferTwo, 0);
        mArgBufferTwo.GetData(args);
        int trianglesRemoved = args[0];

        if (trianglesRemoved > 0)
        {
            int[] indices = new int[trianglesRemoved];
            int[] objects = new int[trianglesRemoved];

            mTriangleRemovalsIndices.GetData(indices);
            mTriangleRemovalsObjects.GetData(objects);
            mNoCopies.Clear();

            for (int i = 0; i < indices.Length; i++)
            {
                if (!mNoCopies.ContainsKey(objects[i]))
                {
                    mNoCopies.Add(objects[i], new List<int>());
                    uniqueShatters.Add(mWalls[objects[i]]);
                }

                if (!mNoCopies[objects[i]].Contains(indices[i]))
                    mNoCopies[objects[i]].Add(indices[i]);
            }

            foreach (KeyValuePair<int, List<int>> list in mNoCopies)
            {
                list.Value.Sort();
                list.Value.Reverse();
            }

            foreach (KeyValuePair<int, List<int>> list in mNoCopies)
            {
                List<int> temp = new List<int>(mWalls[list.Key].GetComponent<MeshFilter>().mesh.triangles);

                //permenately remove triangles
                for (int i = 0; i < list.Value.Count; i++)
                {
                    mWalls[list.Key].RemoveTriangleAtIndex(list.Value[i], ref temp);
                }

                mWalls[list.Key].GetComponent<MeshFilter>().mesh.triangles = temp.ToArray();
                mWalls[list.Key].SetIndexData();
            }

            mTriangleRemovalsIndices.SetData(new int[0]);
            mTriangleRemovalsIndices.SetCounterValue(0);
            mTriangleRemovalsObjects.SetData(new int[0]);
            mTriangleRemovalsObjects.SetCounterValue(0);
        }

        //call post render function on each wall
        foreach (wallShatter shatter in uniqueShatters)
        {
            shatter.CustomPostRender();
        }

        mExpectColRemovals = false;
    }
}
