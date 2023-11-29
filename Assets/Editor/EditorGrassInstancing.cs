using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(GrassInstancing))]
[InitializeOnLoad]
public class EditorGrassInstancing : Editor
{
    GrassInstancing grassPainter;
    readonly string[] toolbarStrings = { "None", "추가", "제거" };

    private void OnEnable()
    {
        grassPainter = (GrassInstancing)target;
    }
    void OnSceneGUI()
    {
        //base
        Handles.color = Color.cyan;
        Handles.DrawWireDisc(grassPainter.hitPosGizmo, grassPainter.hitNormal, grassPainter.brushSize);
        Handles.color = new Color(0, 0.5f, 0.5f, 0.4f);
        Handles.DrawSolidDisc(grassPainter.hitPosGizmo, grassPainter.hitNormal, grassPainter.brushSize);

        if (grassPainter.toolbarInt == 1)
        {
            Handles.color = Color.yellow;
            Handles.DrawWireDisc(grassPainter.hitPosGizmo, grassPainter.hitNormal, grassPainter.brushSize);
            Handles.color = new Color(0.5f, 0f, 0f, 0.4f);
            Handles.DrawSolidDisc(grassPainter.hitPosGizmo, grassPainter.hitNormal, grassPainter.brushSize);
        }
        if (grassPainter.toolbarInt == 2)
        {
            Handles.color = Color.red;
            Handles.DrawWireDisc(grassPainter.hitPosGizmo, grassPainter.hitNormal, grassPainter.brushSize);
            Handles.color = new Color(0.5f, 0.5f, 0f, 0.4f);
            Handles.DrawSolidDisc(grassPainter.hitPosGizmo, grassPainter.hitNormal, grassPainter.brushSize);
        }
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("잔디 에디터", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        grassPainter.toolbarInt = GUILayout.Toolbar(grassPainter.toolbarInt, toolbarStrings);
        
        // 추가
        if (grassPainter.toolbarInt == 1)
        {
        }

        // 제거
        if (grassPainter.toolbarInt == 2)
        {
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("모두 추가"))
        {
            if (EditorUtility.DisplayDialog("정리",
               "진짜 추가?", "확인", "취소"))
            {
                //grassPainter.ClearMesh();
            }
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("모두 제거"))
        {
            if (EditorUtility.DisplayDialog("정리",
               "진짜 정리?", "확인", "취소"))
            {
                //grassPainter.ClearMesh();
            }
        }
    }
}