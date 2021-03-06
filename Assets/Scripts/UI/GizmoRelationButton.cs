using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class GizmoRelationButton : MonoBehaviour
{

    [SerializeField]
    private TextMeshProUGUI _text = default;

    [SerializeField]
    private Button _localButton = default;

    [SerializeField]
    private Button _globalButton = default;



    void Start()
    {
        GizmoRelationManager.onUpdate.AddListener(UpdateVisuals);
        GizmoToolManager.onUpdate.AddListener(UpdateVisuals);
        UpdateVisuals();
    }

    void UpdateVisuals()
    {
        if (_text != null)
            _text.text = GizmoRelationManager.RelationSetting.ToString();

        if (_localButton != null)
            _localButton.interactable =
                GizmoRelationManager.RelationSetting != GizmoRelationManager.RelationSettingEnum.Local 
                && GizmoToolManager.CurrentTool != GizmoToolManager.Tool.Scale; // disable, when scaling is active

        if (_globalButton != null)
            _globalButton.interactable =
                GizmoRelationManager.RelationSetting != GizmoRelationManager.RelationSettingEnum.Global 
                && GizmoToolManager.CurrentTool != GizmoToolManager.Tool.Scale; // disable, when scaling is active
    }

    public void SetNextGizmoRelation()
    {
        GizmoRelationManager.SwitchToNextRelationSetting();
    }

    public void SetRelationLocal()
    {
        GizmoRelationManager.RelationSetting = GizmoRelationManager.RelationSettingEnum.Local;
    }
    public void SetRelationGlobal()
    {
        GizmoRelationManager.RelationSetting = GizmoRelationManager.RelationSettingEnum.Global;
    }
}
