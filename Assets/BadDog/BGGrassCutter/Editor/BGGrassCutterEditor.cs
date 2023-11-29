using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace BadDog
{
    [CustomEditor(typeof(BGGrassCutter))]
    public class BGGrassCutterEditor : Editor
    {
        private SerializedProperty m_TerrainLayer;
        private SerializedProperty m_CutShape;
        private SerializedProperty m_ProcessingType;
        private SerializedProperty m_UpdateStep;

        private SerializedProperty m_CutType;
        private SerializedProperty m_CutLayer;

        private SerializedProperty m_Radius;
        private SerializedProperty m_Degree;
        private SerializedProperty m_Width;
        private SerializedProperty m_Length;
        private SerializedProperty m_MaxHeight;
        private SerializedProperty m_CenterOffsetX;
        private SerializedProperty m_CenterOffsetZ;

        private void OnEnable()
        {
            m_TerrainLayer = serializedObject.FindProperty("terrainLayer");
            m_CutShape = serializedObject.FindProperty("cutShape");
            m_ProcessingType = serializedObject.FindProperty("processingType");
            m_UpdateStep = serializedObject.FindProperty("updateStep");

            m_CutType = serializedObject.FindProperty("cutType");
            m_CutLayer = serializedObject.FindProperty("cutLayer");

            m_Radius = serializedObject.FindProperty("radius");
            m_Degree = serializedObject.FindProperty("degree");
            m_Width = serializedObject.FindProperty("width");
            m_Length = serializedObject.FindProperty("length");
            m_MaxHeight = serializedObject.FindProperty("maxHeight");
            m_CenterOffsetX = serializedObject.FindProperty("centerOffsetX");
            m_CenterOffsetZ = serializedObject.FindProperty("centerOffsetZ");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Rect 1
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Base");

            Rect rect = EditorGUILayout.BeginVertical();
            GUI.Box(rect, "");
            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(m_TerrainLayer, new GUIContent("  Terrain Layer"));
            EditorGUILayout.PropertyField(m_ProcessingType, new GUIContent("  Processing Type"));

            if(m_ProcessingType.enumValueIndex == (int)BGGrassProcessingType.Update || m_ProcessingType.enumValueIndex == (int)BGGrassProcessingType.LateUpdate)
            {
                EditorGUILayout.PropertyField(m_UpdateStep, new GUIContent("  Update Step"));
            }

            EditorGUILayout.Space();
            EditorGUILayout.EndVertical();

            // Rect 2
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Cut Type");

            rect = EditorGUILayout.BeginVertical();
            GUI.Box(rect, "");
            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(m_CutType, new GUIContent("  Cut Type"));
            if(m_CutType.enumValueIndex == (int)BGGrassCutTpye.OneLayer)
            {
                EditorGUILayout.PropertyField(m_CutLayer, new GUIContent("    Cut Layer"));
            }

            EditorGUILayout.Space();
            EditorGUILayout.EndVertical();

            // Rect 3
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Cut Shape");

            rect = EditorGUILayout.BeginVertical();
            GUI.Box(rect, "");
            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(m_CutShape, new GUIContent("  Cut Shape"));

            if(m_CutShape.enumValueIndex == (int)BGGrassCutShape.Circle)
            {
                EditorGUILayout.PropertyField(m_Radius, new GUIContent("  Radius"));
            }
            else if(m_CutShape.enumValueIndex == (int)BGGrassCutShape.Sector)
            {
                EditorGUILayout.PropertyField(m_Radius, new GUIContent("  Radius"));
                EditorGUILayout.PropertyField(m_Degree, new GUIContent("  Degree"));
            }
            else if(m_CutShape.enumValueIndex == (int)BGGrassCutShape.Rect)
            {
                EditorGUILayout.PropertyField(m_Width, new GUIContent("  Width"));
                EditorGUILayout.PropertyField(m_Length, new GUIContent("  Length"));
            }

            EditorGUILayout.PropertyField(m_MaxHeight, new GUIContent("  Max Height"));
            EditorGUILayout.PropertyField(m_CenterOffsetX, new GUIContent("  Center Offset X"));
            EditorGUILayout.PropertyField(m_CenterOffsetZ, new GUIContent("  Center Offset Z"));

            EditorGUILayout.Space();
            EditorGUILayout.EndVertical();

            serializedObject.ApplyModifiedProperties();
        }
    }
}
