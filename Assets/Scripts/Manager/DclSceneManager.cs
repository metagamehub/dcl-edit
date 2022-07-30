using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

#if UNITY_EDITOR
using UnityEditor;
#endif


public class DclSceneManager : Manager, ISerializedFieldToStatic
{
    // Project Path
    public static string DclProjectPath = "";

    //void Start()
    //{
    //    _entityGameObject = entityGameObject;
    //    _entityTemplate = entityTemplate;
    //    GizmoCamera = _gizmoCamera;
    //}

    public void SetupStatics()
    {
        Debug.Log("init scene manager");
        _entityGameObject = entityGameObject;
        _entityTemplate = entityTemplate;
        GizmoCamera = _gizmoCamera;
    }

    public void PrintDclProjectPath()
    {
        Debug.Log("Path: " + DclProjectPath);

        var argsString = "";

        foreach (var arg in System.Environment.GetCommandLineArgs())
        {
            argsString += arg;
            argsString += ", ";
        }
        Debug.Log("Args: " + argsString);
    }


    // read scene.json file 
    public struct SceneJson
    {
        [Serializable]
        public struct Display
        {
            public string title;
            public string description;
            public string navmapThumbnail;
            public string favicon;
        }
        public Display display;

        [Serializable]
        public struct Contact
        {
            public string name;
            public string email;
        }
        public Contact contact;

        public string owner;

        [Serializable]
        public struct Scene
        {
            [SerializeField]
            private string[] parcels;
            [SerializeField]
            private string @base;

            public Vector2Int[] Parcels
            {
                get
                {
                    Vector2Int[] retval = new Vector2Int[parcels.Length];
                    for (var index = 0; index < parcels.Length; index++)
                    {
                        var parcel = parcels[index];
                        retval[index] = parcel.ToVec2Int();
                    }

                    return retval;
                }
            }

            public Vector2Int Base => @base.ToVec2Int();
        }
        public Scene scene;


    }
    public static SceneJson sceneJson;


    // Gizmo sizing 
    public static Camera GizmoCamera;
    [SerializeField]
    private Camera _gizmoCamera;
    //public static float GizmoScale;
    //[SerializeField]
    //private float _gizmoScale;


    // Entities
    public GameObject entityGameObject;
    private static GameObject _entityGameObject;
    public static Entity[] Entities => _entityGameObject.GetComponentsInChildren<Entity>().Where(entity => !entity.doomed).ToArray();
    public static Transform EntityParent => _entityGameObject.transform;

    public GameObject entityTemplate;
    private static GameObject _entityTemplate;
    public static GameObject EntityTemplate => _entityTemplate;

    // Hierarchy View
    public static UnityEvent OnUpdateHierarchy = new UnityEvent();
    //public HierarchyView hierarchyView;
    //private static HierarchyView _hierarchyView;
    public static void ChangedHierarchy()
    {
        //_hierarchyView.UpdateVisuals();
        OnUpdateHierarchy.Invoke();
    }


    // Selection
    public static UnityEvent OnUpdateSelection = new UnityEvent();
    private static Entity _primarySelectedEntity;

    public static Entity PrimarySelectedEntity
    {
        get => _primarySelectedEntity;
        private set
        {
            _primarySelectedEntity = value;

            //foreach (var entity in Entities)
            //{
            //    var isSelected = entity == _primarySelectedEntity;
            //    entity.gizmos.SetActive(isSelected);
            //}

            OnUpdateSelection.Invoke();
        }
    }

    private static List<Entity> _secondarySelectedEntity = new List<Entity>();
    public static IEnumerable<Entity> SecondarySelectedEntity => _secondarySelectedEntity.AsEnumerable();

    public static void SetSelection(Entity entity)
    {
        var currentSecondarySelection = SecondarySelectedEntity.ToList();
        var currentPrimarySelection = PrimarySelectedEntity;

        UndoManager.RecordUndoItem(
            "Selected " + entity.TryGetShownName(),
            () =>
            {
                _secondarySelectedEntity = currentSecondarySelection;
                PrimarySelectedEntity = currentPrimarySelection;
            },
            () =>
            {
                _secondarySelectedEntity.Clear();
                PrimarySelectedEntity = entity;
            });


        DclSceneManager._secondarySelectedEntity.Clear();
        DclSceneManager.PrimarySelectedEntity = entity;
    }

    public static void SetSelectionRaw(Entity entity)
    {
        DclSceneManager._secondarySelectedEntity.Clear();
        DclSceneManager.PrimarySelectedEntity = entity;
    }

    public static void AddSelection(Entity entity)
    {
        if (entity == null)
            return;

        var beforeSecondarySelection = SecondarySelectedEntity.ToList();
        var beforePrimarySelection = PrimarySelectedEntity;


        DclSceneManager._secondarySelectedEntity.Add(DclSceneManager.PrimarySelectedEntity);

        if (DclSceneManager._secondarySelectedEntity.Contains(entity))
            DclSceneManager._secondarySelectedEntity.Remove(entity);

        DclSceneManager.PrimarySelectedEntity = entity;

        var afterSecondarySelection = SecondarySelectedEntity.ToList();
        var afterPrimarySelection = PrimarySelectedEntity;

        UndoManager.RecordUndoItem(
            "Selected " + entity.ShownName,
            () =>
            {
                _secondarySelectedEntity = beforeSecondarySelection;
                PrimarySelectedEntity = beforePrimarySelection;
            },
            () =>
            {
                _secondarySelectedEntity = afterSecondarySelection;
                PrimarySelectedEntity = afterPrimarySelection;
            });

    }

    public static void AddSelectedRaw(Entity entity)
    {
        if (entity == null)
            return;

        DclSceneManager._secondarySelectedEntity.Add(DclSceneManager.PrimarySelectedEntity);

        if (DclSceneManager._secondarySelectedEntity.Contains(entity))
            DclSceneManager._secondarySelectedEntity.Remove(entity);

        DclSceneManager.PrimarySelectedEntity = entity;

    }

    public static IEnumerable<Entity> AllSelectedEntities => _secondarySelectedEntity.Append(PrimarySelectedEntity).Where(entity => entity != null);

    public static IEnumerable<Entity> AllSelectedEntitiesWithoutChildren
    {
        get
        {
            var allSelected = AllSelectedEntities.ToList();
            var selectedWithoutChildren = new List<Entity>();

            // Walk though entire scene tree
            var currentTreeObject = new Stack<SceneTreeObject>();
            currentTreeObject.Push(SceneRoot);

            while (currentTreeObject.Count > 0)
            {
                foreach (var child in currentTreeObject.Pop().Children)
                {
                    if (allSelected.Contains(child))
                    {
                        selectedWithoutChildren.Add(child as Entity); // if current object is selected, add it to returned list
                    }
                    else
                    {
                        currentTreeObject.Push(child); // else push it to the stack to be traversed
                    }
                }
            }

            return selectedWithoutChildren.AsEnumerable();
        }
    }


    public static UnityEvent OnSelectedEntityTransformChange = new UnityEvent();

    public static RootSceneObject SceneRoot => EntityParent.GetComponent<RootSceneObject>();
}

public class EntityArray : List<Entity>
{
    public EntityArray(Entity[] array) : base(array)
    {
    }
}

public static class MyJsonUtil
{
    public static Vector2Int ToVec2Int(this string json)
    {
        var parts = json.Split(',');

        if (parts.Length != 2)
        {
            throw new Exception("trying to parse a non vec2 json field to Vector2Int");
        }

        return new Vector2Int(int.Parse(parts[0]), int.Parse(parts[1]));
    }
}
