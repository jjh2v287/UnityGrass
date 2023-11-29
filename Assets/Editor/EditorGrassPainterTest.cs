using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(GrassPainterTest))]
[InitializeOnLoad]
public class EditorGrassPainterTest : Editor
{
    GrassPainterTest grassPainter;
    readonly string[] toolbarStrings = { "추가", "제거" };

    private void OnEnable()
    {
        grassPainter = (GrassPainterTest)target;
    }
    void OnSceneGUI()
    {
        Handles.color = Color.cyan;
        Handles.DrawWireDisc(grassPainter.hitPosGizmo, grassPainter.hitNormal, grassPainter.brushSize);
        Handles.color = new Color(0, 0.5f, 0.5f, 0.4f);
        Handles.DrawSolidDisc(grassPainter.hitPosGizmo, grassPainter.hitNormal, grassPainter.brushSize);

        if (grassPainter.toolbarInt == 1)
        {
            Handles.color = Color.red;
            Handles.DrawWireDisc(grassPainter.hitPosGizmo, grassPainter.hitNormal, grassPainter.brushSize);
            Handles.color = new Color(0.5f, 0f, 0f, 0.4f);
            Handles.DrawSolidDisc(grassPainter.hitPosGizmo, grassPainter.hitNormal, grassPainter.brushSize);
        }
    }

    public override void OnInspectorGUI()
    {
        EditorGUILayout.LabelField("잔디 제한", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(grassPainter.i.ToString() + "/", EditorStyles.label);
        grassPainter.grassLimit = EditorGUILayout.IntField(grassPainter.grassLimit);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("충돌 설정", EditorStyles.boldLabel);
        LayerMask tempMask = EditorGUILayout.MaskField("충돌 Mask", InternalEditorUtility.LayerMaskToConcatenatedLayersMask(grassPainter.hitMask), InternalEditorUtility.layers);
        grassPainter.hitMask = InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(tempMask);
        LayerMask tempMask2 = EditorGUILayout.MaskField("페인팅 Mask", InternalEditorUtility.LayerMaskToConcatenatedLayersMask(grassPainter.paintMask), InternalEditorUtility.layers);
        grassPainter.paintMask = InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(tempMask2);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("페인팅 상태 (마우스 우클릭으로 조작 하세요)", EditorStyles.boldLabel);
        grassPainter.toolbarInt = GUILayout.Toolbar(grassPainter.toolbarInt, toolbarStrings);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("브러쉬 설정", EditorStyles.boldLabel);

        grassPainter.brushSize = EditorGUILayout.Slider("브러쉬 Size", grassPainter.brushSize, 0.1f, 10f);
        if (grassPainter.toolbarInt == 0)
        {
            grassPainter.normalLimit = EditorGUILayout.Slider("Normal Limit", grassPainter.normalLimit, 0f, 1f);
            grassPainter.density = EditorGUILayout.Slider("밀도", grassPainter.density, 0.1f, 10f);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("너비 , 길이", EditorStyles.boldLabel);
            grassPainter.sizeWidth = EditorGUILayout.Slider("잔디 너비", grassPainter.sizeWidth, 0f, 2f);
            grassPainter.sizeLength = EditorGUILayout.Slider("잔디 길이", grassPainter.sizeLength, 0f, 2f);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("컬러", EditorStyles.boldLabel);
            grassPainter.AdjustedColor = EditorGUILayout.ColorField("브러쉬 컬러", grassPainter.AdjustedColor);
            EditorGUILayout.LabelField("컬러 랜덤 변화 값", EditorStyles.boldLabel);
            grassPainter.rangeR = EditorGUILayout.Slider("Red", grassPainter.rangeR, 0f, 1f);
            grassPainter.rangeG = EditorGUILayout.Slider("Green", grassPainter.rangeG, 0f, 1f);
            grassPainter.rangeB = EditorGUILayout.Slider("Blue", grassPainter.rangeB, 0f, 1f);
        }

        if (GUILayout.Button("모두 제거"))
        {
            if (EditorUtility.DisplayDialog("정리",
               "진짜 정리?", "확인", "취소"))
            {
                grassPainter.ClearMesh();
            }
        }
    }

}
