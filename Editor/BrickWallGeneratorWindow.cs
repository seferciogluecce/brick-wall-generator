using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal sealed class BrickWallGeneratorWindow : EditorWindow
{
    private const string MenuPath = "Tools/Brick Wall Generator";
    private const string WindowTitle = "Brick Wall Generator";
    private const string DefaultWallName = "Brick Wall";
    private const string SummaryText = "Generate a configurable brick wall within the assigned bounds.";
    private const string AssignBoundsObjectMessage = "Assign a Bounds Object.";
    private const float MinimumDimension = 0.0001f;

    private GameObject boundsObject;
    private int columns = 5;
    private int rows = 6;
    private float horizontalGap = 0.03f;
    private float verticalGap = 0.03f;
    private bool shiftAlternateRows = true;
    private float shiftAmount = 0.5f;
    private bool fillShiftedEnds = true;
    private string wallName = DefaultWallName;
    private Material materialOverride;
    private bool addColliders = true;
    private bool hideBoundsObject = true;

    private StatusMessage status = StatusMessage.Info(AssignBoundsObjectMessage);
    private bool hasSuccessStatus;

    [MenuItem(MenuPath, false, 222)]
    private static void Open()
    {
        BrickWallGeneratorWindow window = GetWindow<BrickWallGeneratorWindow>(WindowTitle);
        window.minSize = new Vector2(360f, 430f);
        window.Show();
    }

    private void OnEnable()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        UpdateStatus();
    }

    private void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
    }

    private void OnSelectionChange()
    {
        Repaint();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(SummaryText, EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.Space(4f);

        EditorGUI.BeginChangeCheck();

        DrawBoundsSection();
        EditorGUILayout.Space(8f);
        DrawLayoutSection();
        EditorGUILayout.Space(8f);
        DrawOutputSection();
        EditorGUILayout.Space(8f);

        if (EditorGUI.EndChangeCheck())
        {
            ClearSuccess();
            UpdateStatus();
        }

        ValidationResult validation = ValidateCurrentState();
        if (!hasSuccessStatus)
            status = validation.Status;

        DrawStatus(status);

        using (new EditorGUI.DisabledScope(!validation.CanGenerate))
        {
            if (GUILayout.Button("Generate Brick Wall", GUILayout.Height(28f)))
                GenerateBrickWall();
        }
    }

    private void DrawBoundsSection()
    {
        EditorGUILayout.LabelField("Bounds", EditorStyles.boldLabel);

        boundsObject = (GameObject)EditorGUILayout.ObjectField(
            new GUIContent("Bounds Object", "Scene object whose direct MeshRenderer bounds define the wall volume, orientation, depth, and fallback material."),
            boundsObject,
            typeof(GameObject),
            true);

        using (new EditorGUI.DisabledScope(!CanUseSelection()))
        {
            if (GUILayout.Button("Use Selected"))
            {
                boundsObject = Selection.activeGameObject;
                ClearSuccess();
                UpdateStatus();
            }
        }
    }

    private void DrawLayoutSection()
    {
        EditorGUILayout.LabelField("Layout", EditorStyles.boldLabel);

        columns = EditorGUILayout.IntSlider(new GUIContent("Columns", "Base number of bricks across each unshifted row. Shifted edge bricks may change the final brick count."), columns, 1, 64);
        rows = EditorGUILayout.IntSlider(new GUIContent("Rows", "Number of brick rows within the bounds. Alternate-row shifting is disabled when there is only one row."), rows, 1, 64);
        horizontalGap = Mathf.Max(0f, EditorGUILayout.FloatField(new GUIContent("Horizontal Gap", "Horizontal space between base columns. The wall width stays fixed, so larger gaps shrink bricks."), horizontalGap));
        verticalGap = Mathf.Max(0f, EditorGUILayout.FloatField(new GUIContent("Vertical Gap", "Vertical space between rows. The wall height stays fixed, so larger gaps shrink bricks."), verticalGap));
        shiftAlternateRows = EditorGUILayout.Toggle(new GUIContent("Shift Alternate Rows", "Offset every other row in positive local X to create a staggered brick pattern."), shiftAlternateRows);

        bool canShift = shiftAlternateRows && rows >= 2;
        using (new EditorGUI.DisabledScope(!canShift))
        {
            shiftAmount = EditorGUILayout.Slider(new GUIContent("Shift Amount", "Percentage of the horizontal brick pitch used to offset shifted rows."), shiftAmount, 0f, 1f);
        }

        using (new EditorGUI.DisabledScope(!canShift || shiftAmount <= MinimumDimension))
        {
            fillShiftedEnds = EditorGUILayout.Toggle(new GUIContent("Fill Shifted Ends", "Add clipped partial bricks at shifted row edges so the row fills the bounds without extending outside."), fillShiftedEnds);
        }
    }

    private void DrawOutputSection()
    {
        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);

        wallName = EditorGUILayout.TextField(new GUIContent("Wall Name", "Name for the generated wall parent. Empty names fall back to Brick Wall and duplicates are made unique."), wallName);
        materialOverride = (Material)EditorGUILayout.ObjectField(new GUIContent("Material (Optional)", "Material applied to all generated bricks. If empty, the Bounds Object's first direct renderer material is used."), materialOverride, typeof(Material), false);
        addColliders = EditorGUILayout.Toggle(new GUIContent("Add Colliders", "Keep one BoxCollider on each generated brick. Turn off to create render-only bricks."), addColliders);
        hideBoundsObject = EditorGUILayout.Toggle(new GUIContent("Hide Bounds Object", "Deactivate the Bounds Object after a successful generation. Undo restores its previous active state."), hideBoundsObject);
    }

    private static void DrawStatus(StatusMessage message)
    {
        if (message.Severity == StatusSeverity.Info && message.Text == AssignBoundsObjectMessage)
        {
            EditorGUILayout.LabelField(message.Text, EditorStyles.miniLabel);
            return;
        }

        MessageType messageType = MessageType.None;
        if (message.Severity == StatusSeverity.Info)
            messageType = MessageType.Info;
        else if (message.Severity == StatusSeverity.Warning)
            messageType = MessageType.Warning;
        else if (message.Severity == StatusSeverity.Error)
            messageType = MessageType.Error;

        DrawInlineMessage(message.Text, messageType);
    }

    private static void DrawInlineMessage(string message, MessageType messageType)
    {
        if (string.IsNullOrEmpty(message))
            return;

        GUIStyle style = EditorStyles.wordWrappedMiniLabel;
        if (messageType == MessageType.Warning)
            style = EditorStyles.miniBoldLabel;
        else if (messageType == MessageType.Error)
            style = EditorStyles.boldLabel;

        EditorGUILayout.LabelField(message, style);
    }

    private bool CanUseSelection()
    {
        GameObject selected = Selection.activeGameObject;
        return selected != null && selected.scene.IsValid();
    }

    private void GenerateBrickWall()
    {
        ValidationResult validation = ValidateCurrentState();
        if (!validation.CanGenerate)
        {
            status = validation.Status;
            hasSuccessStatus = false;
            Repaint();
            return;
        }

        GameObject source = boundsObject;
        bool sourceWasActive = source.activeSelf;
        GameObject wallParent = null;

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Generate Brick Wall");

        try
        {
            SourceInfo sourceInfo = validation.SourceInfo;
            Material material = ResolveMaterial(sourceInfo.Renderer);
            string resolvedName = string.IsNullOrWhiteSpace(wallName) ? DefaultWallName : wallName.Trim();

            string uniqueName = GameObjectUtility.GetUniqueNameForSibling(source.transform.parent, resolvedName);
            wallParent = new GameObject(uniqueName);
            Undo.RegisterCreatedObjectUndo(wallParent, "Create brick wall parent");
            wallParent.transform.SetParent(source.transform.parent, false);
            MoveToScene(wallParent, source.scene);
            wallParent.transform.SetPositionAndRotation(sourceInfo.Center, sourceInfo.Rotation);
            wallParent.transform.localScale = Vector3.one;
            wallParent.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);

            List<GameObject> createdBricks = GenerateBricks(wallParent.transform, sourceInfo.Size, material);

            if (hideBoundsObject)
            {
                Undo.RecordObject(source, "Hide bounds object");
                source.SetActive(false);
                EditorUtility.SetDirty(source);
            }

            if (wallParent.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(wallParent.scene);

            Selection.activeGameObject = wallParent;
            Undo.CollapseUndoOperations(undoGroup);

            status = StatusMessage.Info($"Brick wall created with {createdBricks.Count} bricks ({rows} rows × {columns} columns).");
            hasSuccessStatus = true;
            SceneView.RepaintAll();
        }
        catch (Exception ex)
        {
            if (wallParent != null)
                Undo.DestroyObjectImmediate(wallParent);

            if (source != null && source.activeSelf != sourceWasActive)
                source.SetActive(sourceWasActive);

            Undo.CollapseUndoOperations(undoGroup);
            status = StatusMessage.Error($"Error: {ex.Message}");
            hasSuccessStatus = false;
            Repaint();
        }
    }

    private List<GameObject> GenerateBricks(Transform parent, Vector3 size, Material material)
    {
        int columnCount = Mathf.Clamp(columns, 1, 64);
        int rowCount = Mathf.Clamp(rows, 1, 64);
        float gapX = Mathf.Max(0f, horizontalGap);
        float gapY = Mathf.Max(0f, verticalGap);

        float brickWidth = (size.x - gapX * (columnCount - 1)) / columnCount;
        float brickHeight = (size.y - gapY * (rowCount - 1)) / rowCount;
        float pitchX = brickWidth + gapX;
        float pitchY = brickHeight + gapY;
        float minX = size.x * -0.5f;
        float maxX = size.x * 0.5f;
        float startX = minX + brickWidth * 0.5f;
        float startY = size.y * -0.5f + brickHeight * 0.5f;

        List<GameObject> createdBricks = new List<GameObject>(rowCount * (columnCount + 2));

        for (int row = 0; row < rowCount; row++)
        {
            bool shiftedRow = shiftAlternateRows && rowCount >= 2 && row % 2 == 1;
            float shift = shiftedRow ? pitchX * Mathf.Clamp01(shiftAmount) : 0f;
            bool fillEnds = shiftedRow && fillShiftedEnds && shift > MinimumDimension;
            int firstColumn = fillEnds ? -1 : 0;

            for (int column = firstColumn; column < columnCount; column++)
            {
                float centerX = startX + column * pitchX + shift;
                float brickMinX = centerX - brickWidth * 0.5f;
                float brickMaxX = centerX + brickWidth * 0.5f;

                if (!fillEnds && (brickMinX < minX - MinimumDimension || brickMaxX > maxX + MinimumDimension))
                    continue;

                float clippedMinX = Mathf.Max(brickMinX, minX);
                float clippedMaxX = Mathf.Min(brickMaxX, maxX);
                float clippedWidth = clippedMaxX - clippedMinX;
                if (clippedWidth <= MinimumDimension)
                    continue;

                GameObject brick = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Undo.RegisterCreatedObjectUndo(brick, "Create brick");
                brick.name = GetBrickName(row, createdBricks.Count);
                brick.transform.SetParent(parent, false);
                brick.transform.localPosition = new Vector3((clippedMinX + clippedMaxX) * 0.5f, startY + row * pitchY, 0f);
                brick.transform.localRotation = Quaternion.identity;
                brick.transform.localScale = new Vector3(clippedWidth, brickHeight, size.z);

                if (!addColliders)
                {
                    BoxCollider collider = brick.GetComponent<BoxCollider>();
                    if (collider != null)
                        DestroyImmediate(collider);
                }

                if (material != null)
                {
                    Renderer renderer = brick.GetComponent<Renderer>();
                    if (renderer != null)
                        renderer.sharedMaterial = material;
                }

                createdBricks.Add(brick);
            }
        }

        return createdBricks;
    }

    private static string GetBrickName(int row, int generatedIndex)
    {
        return $"Brick_R{row + 1:00}_C{generatedIndex + 1:00}";
    }

    private Material ResolveMaterial(MeshRenderer renderer)
    {
        if (materialOverride != null)
            return materialOverride;

        Material[] sourceMaterials = renderer.sharedMaterials;
        return sourceMaterials != null && sourceMaterials.Length > 0 ? sourceMaterials[0] : null;
    }

    private ValidationResult ValidateCurrentState()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return ValidationResult.Blocked(StatusMessage.Info("Generation is disabled in Play Mode."));

        if (boundsObject == null)
            return ValidationResult.Blocked(StatusMessage.Info(AssignBoundsObjectMessage));

        if (!boundsObject.scene.IsValid())
            return ValidationResult.Blocked(StatusMessage.Warning("Bounds Object must be a scene object in the current editable context."));

        if (!IsInCurrentEditableContext(boundsObject))
            return ValidationResult.Blocked(StatusMessage.Warning("Bounds Object is outside the current editable context."));

        if (!boundsObject.activeInHierarchy)
            return ValidationResult.Blocked(StatusMessage.Warning("Bounds Object must be active."));

        MeshFilter meshFilter = boundsObject.GetComponent<MeshFilter>();
        if (meshFilter == null)
            return ValidationResult.Blocked(StatusMessage.Warning("Bounds Object must have a direct MeshFilter."));

        if (meshFilter.sharedMesh == null)
            return ValidationResult.Blocked(StatusMessage.Warning("Bounds Object MeshFilter must have a valid mesh."));

        MeshRenderer meshRenderer = boundsObject.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
            return ValidationResult.Blocked(StatusMessage.Warning("Bounds Object must have a direct MeshRenderer."));

        if (!meshRenderer.enabled)
            return ValidationResult.Blocked(StatusMessage.Warning("Bounds Object MeshRenderer must be enabled."));

        if (HasUnsupportedScale(boundsObject.transform))
            return ValidationResult.Blocked(StatusMessage.Warning("Negative or zero scale on the Bounds Object or its ancestors is unsupported."));

        SourceInfo sourceInfo = CalculateSourceInfo(boundsObject.transform, meshRenderer);
        if (!HasPositiveDimensions(sourceInfo.Size))
            return ValidationResult.Blocked(StatusMessage.Warning("Bounds Object must have valid positive rendered dimensions."));

        int columnCount = Mathf.Clamp(columns, 1, 64);
        int rowCount = Mathf.Clamp(rows, 1, 64);
        float brickWidth = (sourceInfo.Size.x - Mathf.Max(0f, horizontalGap) * (columnCount - 1)) / columnCount;
        float brickHeight = (sourceInfo.Size.y - Mathf.Max(0f, verticalGap) * (rowCount - 1)) / rowCount;
        if (brickWidth <= MinimumDimension || brickHeight <= MinimumDimension)
            return ValidationResult.Blocked(StatusMessage.Warning("Gaps are too large for the Bounds Object dimensions, rows, and columns."));

        return ValidationResult.Valid(sourceInfo);
    }

    private static SourceInfo CalculateSourceInfo(Transform source, MeshRenderer renderer)
    {
        Bounds localBounds = renderer.localBounds;
        Vector3 worldCenter = source.TransformPoint(localBounds.center);
        Quaternion wallRotation = source.rotation;
        Matrix4x4 wallLocalToWorld = Matrix4x4.TRS(worldCenter, wallRotation, Vector3.one);

        if (source.parent != null)
        {
            Matrix4x4 parentWorldToLocal = source.parent.worldToLocalMatrix;
            Vector3 parentLocalPosition = parentWorldToLocal.MultiplyPoint3x4(worldCenter);
            Quaternion parentLocalRotation = Quaternion.Inverse(source.parent.rotation) * wallRotation;
            wallLocalToWorld = source.parent.localToWorldMatrix * Matrix4x4.TRS(parentLocalPosition, parentLocalRotation, Vector3.one);
        }

        Matrix4x4 wallWorldToLocal = wallLocalToWorld.inverse;
        Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

        Vector3 boundsMin = localBounds.min;
        Vector3 boundsMax = localBounds.max;

        for (int x = 0; x <= 1; x++)
        {
            for (int y = 0; y <= 1; y++)
            {
                for (int z = 0; z <= 1; z++)
                {
                    Vector3 localCorner = new Vector3(
                        x == 0 ? boundsMin.x : boundsMax.x,
                        y == 0 ? boundsMin.y : boundsMax.y,
                        z == 0 ? boundsMin.z : boundsMax.z);

                    Vector3 wallLocalCorner = wallWorldToLocal.MultiplyPoint3x4(source.TransformPoint(localCorner));
                    min = Vector3.Min(min, wallLocalCorner);
                    max = Vector3.Max(max, wallLocalCorner);
                }
            }
        }

        return new SourceInfo(worldCenter, wallRotation, max - min, renderer);
    }

    private static bool IsInCurrentEditableContext(GameObject gameObject)
    {
        PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
        if (prefabStage != null)
            return gameObject.scene == prefabStage.scene;

        return gameObject.scene.IsValid() && gameObject.scene.isLoaded;
    }

    private static bool HasUnsupportedScale(Transform transform)
    {
        for (Transform current = transform; current != null; current = current.parent)
        {
            Vector3 scale = current.localScale;
            if (scale.x <= 0f || scale.y <= 0f || scale.z <= 0f)
                return true;
        }

        return false;
    }

    private static bool HasPositiveDimensions(Vector3 size)
    {
        return size.x > MinimumDimension && size.y > MinimumDimension && size.z > MinimumDimension;
    }

    private static void MoveToScene(GameObject gameObject, Scene scene)
    {
        if (scene.IsValid() && gameObject.scene != scene)
            SceneManager.MoveGameObjectToScene(gameObject, scene);
    }

    private void ClearSuccess()
    {
        hasSuccessStatus = false;
    }

    private void UpdateStatus()
    {
        if (!hasSuccessStatus)
            status = ValidateCurrentState().Status;
    }

    private void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        ClearSuccess();
        UpdateStatus();
        Repaint();
    }

    private readonly struct SourceInfo
    {
        public SourceInfo(Vector3 center, Quaternion rotation, Vector3 size, MeshRenderer renderer)
        {
            Center = center;
            Rotation = rotation;
            Size = size;
            Renderer = renderer;
        }

        public Vector3 Center { get; }
        public Quaternion Rotation { get; }
        public Vector3 Size { get; }
        public MeshRenderer Renderer { get; }
    }

    private readonly struct ValidationResult
    {
        private ValidationResult(bool canGenerate, StatusMessage status, SourceInfo sourceInfo)
        {
            CanGenerate = canGenerate;
            Status = status;
            SourceInfo = sourceInfo;
        }

        public bool CanGenerate { get; }
        public StatusMessage Status { get; }
        public SourceInfo SourceInfo { get; }

        public static ValidationResult Valid(SourceInfo sourceInfo)
        {
            return new ValidationResult(true, StatusMessage.Info("Ready to generate."), sourceInfo);
        }

        public static ValidationResult Blocked(StatusMessage status)
        {
            return new ValidationResult(false, status, default);
        }
    }

    private readonly struct StatusMessage
    {
        private StatusMessage(StatusSeverity severity, string text)
        {
            Severity = severity;
            Text = text;
        }

        public StatusSeverity Severity { get; }
        public string Text { get; }

        public static StatusMessage Info(string text)
        {
            return new StatusMessage(StatusSeverity.Info, text);
        }

        public static StatusMessage Warning(string text)
        {
            return new StatusMessage(StatusSeverity.Warning, text);
        }

        public static StatusMessage Error(string text)
        {
            return new StatusMessage(StatusSeverity.Error, text);
        }
    }

    private enum StatusSeverity
    {
        Info,
        Warning,
        Error
    }
}
