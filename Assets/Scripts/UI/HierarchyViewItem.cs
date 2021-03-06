using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class HierarchyViewItem : MonoBehaviour,ISerializedFieldToStatic,IPointerClickHandler,IBeginDragHandler,IEndDragHandler,IDragHandler
{
    [Header("Pointer Handler")]
    [Space(20,order = 100)]

    public TextMeshProUGUI nameText;

    [NonSerialized]
    public Entity entity;

    //[NonSerialized]
    //public int indentLevel = 0;

    private bool IsPrimarySelected => entity == DclSceneManager.PrimarySelectedEntity;
    private bool IsSecondarySelected => DclSceneManager.SecondarySelectedEntity.Contains(entity);


    [SerializeField]
    private Color defaultWhite = new Color(0.8980393f, 0.9058824f, 0.9215687f);

    [SerializeField]
    private Color floatingColor = new Color(0.8980393f, 0.9058824f, 0.9215687f);

    [SerializeField]
    private Color primarySelect = new Color(0.5764706f, 0.7725491f, 0.9921569f);

    [SerializeField]
    private Color secondarySelect = new Color(0.5764706f, 0.7725491f, 0.9921569f);
    

    private static Color _defaultWhite;
    private static Color _primarySelectionBlue;
    private static Color _secondarySelectionBlue;


    [SerializeField]
    private CollapseArrowUI _collapseArrow;

    
    public void SetupStatics()
    {
        _defaultWhite = defaultWhite;
        _primarySelectionBlue = primarySelect;
        _secondarySelectionBlue = secondarySelect;
    }

    public void UpdateVisuals()
    {
        nameText.text = entity.ShownName;
        //nameText.margin = new Vector4((entity.Level + 2) * 20f,0,0,0);
        nameText.color = 
            IsPrimarySelected ? _primarySelectionBlue : 
            IsSecondarySelected ? _secondarySelectionBlue : _defaultWhite;

        GetComponent<RectTransform>().SetLeft(entity.Level*20f);
        
        _collapseArrow.gameObject.SetActive(entity.AllChildCount > 0);

        _collapseArrow.gameObject.GetComponent<RectTransform>().eulerAngles = 
            entity.CollapsedChildren ? 
                new Vector3(0, 0, 90):
                new Vector3(0, 0, 0);
    }

    private class ChangeHierarchyUndoRecorder
    {
        private SceneTreeObject oldParent;
        private SceneTreeObject oldPrevious;
        public ChangeHierarchyUndoRecorder(SceneTreeObject entity)
        {
            this.oldParent = entity.Parent;
            this.oldPrevious = entity.PreviousSibling;
        }

        public void StopRecording(SceneTreeObject entity)
        {
            var currentParent = entity.Parent;
            var currentPrevious = entity.PreviousSibling;


            UndoManager.RecordUndoItem(
                "Changed Hierarchy",
                () =>
                {
                    entity.Parent = oldParent;
                    entity.HierarchyOrder = oldPrevious.HierarchyOrder+0.5f;
                    
                    NormalizeHierarchyOrderValues.Normalize();
                    DclSceneManager.OnUpdateHierarchy.Invoke();
                },
                () =>
                {
                    entity.Parent = currentParent;
                    entity.HierarchyOrder = currentPrevious.HierarchyOrder + 0.5f;
                    
                    NormalizeHierarchyOrderValues.Normalize();
                    DclSceneManager.OnUpdateHierarchy.Invoke();
                }
                );
        }

    }

    public void MoveBefore(Entity otherEntity)
    {
        Debug.Log("Move before "+otherEntity.ShownName);
        var undoItemReorder = new ChangeHierarchyUndoRecorder(entity);
        entity.Parent = otherEntity.Parent;
        entity.HierarchyOrder = otherEntity.HierarchyOrder-0.5f;
        NormalizeHierarchyOrderValues.Normalize();
        DclSceneManager.OnUpdateHierarchy.Invoke();
        undoItemReorder.StopRecording(entity);

    }

    public void MoveAfter(Entity otherEntity)
    {
        Debug.Log("Move after "+otherEntity.ShownName);
        
        var undoItemReorder = new ChangeHierarchyUndoRecorder(entity);
        if (otherEntity.AllChildCount > 0)
        {
            entity.Parent = otherEntity;
            entity.HierarchyOrder = -1;
        }
        else
        {
            entity.Parent = otherEntity.Parent;
            entity.HierarchyOrder = otherEntity.HierarchyOrder+0.5f;
        }

        NormalizeHierarchyOrderValues.Normalize();
        DclSceneManager.OnUpdateHierarchy.Invoke();
        undoItemReorder.StopRecording(entity);
    }

    public void MoveLast()
    {
        Debug.Log("Move to last place");
        var undoItemReorder = new ChangeHierarchyUndoRecorder(entity);
        entity.Parent = DclSceneManager.SceneRoot;
        entity.HierarchyOrder = float.MaxValue;
        NormalizeHierarchyOrderValues.Normalize();
        DclSceneManager.OnUpdateHierarchy.Invoke();
        undoItemReorder.StopRecording(entity);
    }

    public void MoveToChild(Entity otherEntity)
    {
        Debug.Log("Move to child of "+otherEntity.ShownName);
        var undoItemReorder = new ChangeHierarchyUndoRecorder(entity);
        entity.Parent = otherEntity;
        entity.HierarchyOrder = float.MaxValue;
        NormalizeHierarchyOrderValues.Normalize();
        DclSceneManager.OnUpdateHierarchy.Invoke();
        undoItemReorder.StopRecording(entity);
    }

    public void SelectEntity()
    {
        /*Instantiate(gameObject, CanvasManager.FloatingListItemParent.transform, true);*/
        var pressingControl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

        if (!pressingControl)
        {
            DclSceneManager.SetSelection(entity);
        }
        else
        {
            DclSceneManager.AddSelection(entity);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        //Debug.Log("OnPointerClick");
        SelectEntity();
    }

    private GameObject _dragCopy = null;
    public void OnBeginDrag(PointerEventData eventData)
    {
        Debug.Log("OnBeginDrag");

        GetComponentInParent<HierarchyView>().EnableDropZones();

        _dragCopy = Instantiate(gameObject, CanvasManager.FloatingListItemParent.transform, true);
        var cg = _dragCopy.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable = false;
        
        nameText.color = floatingColor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        Debug.Log("OnEndDrag");
        
        GetComponentInParent<HierarchyView>().DisableDropZones();

        Destroy(_dragCopy);
        _dragCopy = null;
        
        nameText.color = 
            IsPrimarySelected ? _primarySelectionBlue : 
            IsSecondarySelected ? _secondarySelectionBlue : _defaultWhite;

        Debug.Log(DclSceneManager.EntityParent.GetComponent<RootSceneObject>().GetTree());
    }
    
    public void OnDrag(PointerEventData eventData)
    {
        //Debug.Log("OnDrag");
        if(_dragCopy != null)
        {
            _dragCopy.GetComponent<RectTransform>().anchoredPosition += eventData.delta / CanvasManager.MainCanvas.scaleFactor;
        }


        var indicatorWasSet = 
            eventData.hovered // All hovered objects
            .Where(o => o.TryGetComponent<HierarchySorterDropArea>(out _)) // The hovered objects that contain a HierarchySorterDropArea
            .Select(o => o.GetComponent<HierarchySorterDropArea>()) // The HierarchySorterDropAreas from those objects
            .Any(dropArea => // Set indicator to the fist available HierarchySorterDropArea
            {
                GetComponentInParent<HierarchyView>().dropIndicator.SetIndicatorToItem(dropArea.ownViewItem, dropArea.place);
                return true;
            });

        if (!indicatorWasSet) // Hide indicator when no HierarchySorterDropArea was hovered
        {
            GetComponentInParent<HierarchyView>().dropIndicator.HideIndicator();
        }
    }

    [SerializeField]
    private GameObject _dropZone;
    
    public void SetDropZoneActive(bool value)
    {
        _dropZone.SetActive(value);
    }
}
