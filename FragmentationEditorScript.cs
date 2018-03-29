using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;

/// <summary>
/// Editor script used to help generate everything necessary for breakable walls
/// </summary>
[CustomEditor(typeof(wallShatter))]
public class FragmentationEditorScript : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        serializedObject.UpdateIfRequiredOrScript(); //Might need to be just Update

        wallShatter wall = (wallShatter)target;
        if (GUILayout.Button("Generate Collider"))
        {
            wall.InitializeData();
        }
    }
}
#endif