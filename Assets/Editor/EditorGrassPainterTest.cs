using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(GrassPainterTest))]
[InitializeOnLoad]
public class EditorGrassPainterTest : Editor
{
    GrassPainterTest grassPainter;
    readonly string[] toolbarStrings = { "�߰�", "����" };

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
        EditorGUILayout.LabelField("�ܵ� ����", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(grassPainter.i.ToString() + "/", EditorStyles.label);
        grassPainter.grassLimit = EditorGUILayout.IntField(grassPainter.grassLimit);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("�浹 ����", EditorStyles.boldLabel);
        LayerMask tempMask = EditorGUILayout.MaskField("�浹 Mask", InternalEditorUtility.LayerMaskToConcatenatedLayersMask(grassPainter.hitMask), InternalEditorUtility.layers);
        grassPainter.hitMask = InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(tempMask);
        LayerMask tempMask2 = EditorGUILayout.MaskField("������ Mask", InternalEditorUtility.LayerMaskToConcatenatedLayersMask(grassPainter.paintMask), InternalEditorUtility.layers);
        grassPainter.paintMask = InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(tempMask2);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("������ ���� (���콺 ��Ŭ������ ���� �ϼ���)", EditorStyles.boldLabel);
        grassPainter.toolbarInt = GUILayout.Toolbar(grassPainter.toolbarInt, toolbarStrings);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("�귯�� ����", EditorStyles.boldLabel);

        grassPainter.brushSize = EditorGUILayout.Slider("�귯�� Size", grassPainter.brushSize, 0.1f, 10f);
        if (grassPainter.toolbarInt == 0)
        {
            grassPainter.normalLimit = EditorGUILayout.Slider("Normal Limit", grassPainter.normalLimit, 0f, 1f);
            grassPainter.density = EditorGUILayout.Slider("�е�", grassPainter.density, 0.1f, 10f);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("�ʺ� , ����", EditorStyles.boldLabel);
            grassPainter.sizeWidth = EditorGUILayout.Slider("�ܵ� �ʺ�", grassPainter.sizeWidth, 0f, 2f);
            grassPainter.sizeLength = EditorGUILayout.Slider("�ܵ� ����", grassPainter.sizeLength, 0f, 2f);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("�÷�", EditorStyles.boldLabel);
            grassPainter.AdjustedColor = EditorGUILayout.ColorField("�귯�� �÷�", grassPainter.AdjustedColor);
            EditorGUILayout.LabelField("�÷� ���� ��ȭ ��", EditorStyles.boldLabel);
            grassPainter.rangeR = EditorGUILayout.Slider("Red", grassPainter.rangeR, 0f, 1f);
            grassPainter.rangeG = EditorGUILayout.Slider("Green", grassPainter.rangeG, 0f, 1f);
            grassPainter.rangeB = EditorGUILayout.Slider("Blue", grassPainter.rangeB, 0f, 1f);
        }

        if (GUILayout.Button("��� ����"))
        {
            if (EditorUtility.DisplayDialog("����",
               "��¥ ����?", "Ȯ��", "���"))
            {
                grassPainter.ClearMesh();
            }
        }
    }

}
