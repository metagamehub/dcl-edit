using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;
using Random = UnityEngine.Random;

public class Interface3DManager : MonoBehaviour
{
    public Camera gizmoCamera;
    public RightClickCameraController cameraController;

    //private Material _lastHovered = null;
    private Interface3DHover _lastHoveredVisualIndicator = null;

    private EntityManipulator _activeManipulator;
    private Vector3? _lastMousePosition = null;
    private Plane _activeManipulatorPlane;


    private StateMachine _interfaceStateMachine;
    private StateMachine.State _freeMouseState;
    private StateMachine.State _cameraWasdMovingState;
    private StateMachine.State _cameraMouseZoomingState;
    private StateMachine.State _cameraRotateAroundState;
    private StateMachine.State _cameraSlideMovingState;
    private StateMachine.State _holdingManipulatorState;


    [NonSerialized]
    public static UnityEvent onTransformChange = new UnityEvent();

    private Vector3? _mouseInWorldPoint;

    void Start()
    {
        // This State is normally active, when the user just hovers the mouse over the 3D view
        _freeMouseState = new StateMachine.State("Free mouse state");
        _freeMouseState.OnStateExit = _ =>
        {
            if (_lastHoveredVisualIndicator != null)
                _lastHoveredVisualIndicator.EndHover();
            _lastHoveredVisualIndicator = null;
        };
        _freeMouseState.OnStateUpdate = state =>
        {
            // Get the ray from the camera, where the mouse currently is
            var mouseRay = gizmoCamera.ViewportPointToRay(gizmoCamera.ScreenToViewportPoint(Input.mousePosition));

            // Figure out, if the mouse is over the Game window
            var isMouseOverGameWindow =
                !(0 > Input.mousePosition.x ||
                  0 > Input.mousePosition.y ||
                  Screen.width < Input.mousePosition.x ||
                  Screen.height < Input.mousePosition.y);

            // Figure out, if the mouse is over the 3D viewport (not hovering over any UI)
            var isMouseIn3DView = !EventSystem.current.IsPointerOverGameObject() && isMouseOverGameWindow;

            // Figuring out, what the mouse currently hovers
            Interface3DHover hoveredVisualIndicator = null;
            EntityManipulator hoveredManipulator = null;
            Entity hoveredEntity = null;
            _mouseInWorldPoint = null;

            // Do the following block only, if the mouse is over the 3D view
            if (isMouseIn3DView)
            {
                // First check, if mouse is hovering over Gizmo
                if (Physics.Raycast(mouseRay, out RaycastHit hitInfoGizmos, 10000, LayerMask.GetMask("Gizmos")))
                {
                    hoveredManipulator = hitInfoGizmos.transform.GetComponent<EntityManipulator>();
                    if (hitInfoGizmos.transform.gameObject.TryGetComponent(out Interface3DHover hoverIndicator))
                    {
                        hoveredVisualIndicator = hoverIndicator; // set the hover indicator if one is present
                    }

                    _mouseInWorldPoint = hitInfoGizmos.point;

                }
                // Secondly check, if mouse is hovering over Entity
                else if (Physics.Raycast(mouseRay, out RaycastHit hitInfoEntity, 10000, LayerMask.GetMask("Entity")))
                {
                    hoveredEntity = hitInfoEntity.transform.GetComponentInParent<Entity>();
                    if (hoveredEntity.gameObject.TryGetComponent(out Interface3DHover hoverIndicator))
                    {
                        hoveredVisualIndicator = hoverIndicator; // set the hover indicator if one is present
                    }

                    _mouseInWorldPoint = hitInfoEntity.point;
                }

            }

            if (_mouseInWorldPoint == null)
            {
                var groundPlane = new Plane(Vector3.up, Vector3.zero);
                groundPlane.Raycast(mouseRay, out float enter);
                if (enter > 2000 || enter < 0)
                {
                    _mouseInWorldPoint = mouseRay.GetPoint(10);
                }
                else
                {
                    _mouseInWorldPoint = mouseRay.GetPoint(enter);
                }
            }

            // Update hover indicator when necessary
            if (hoveredVisualIndicator != _lastHoveredVisualIndicator)
            {
                if (hoveredVisualIndicator != null)
                    hoveredVisualIndicator.StartHover();
                if (_lastHoveredVisualIndicator != null)
                    _lastHoveredVisualIndicator.EndHover();
                _lastHoveredVisualIndicator = hoveredVisualIndicator;
            }

            var pressingAlt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

            // When Left mouse button is clicked, do necessary actions
            if (Input.GetMouseButtonDown((int)MouseButton.LeftMouse) && !pressingAlt && isMouseIn3DView)
            {
                if (hoveredManipulator != null)
                {
                    _activeManipulator = hoveredManipulator;
                    _activeManipulatorPlane = _activeManipulator.GetPlane(gizmoCamera);
                    _interfaceStateMachine.ActiveState = _holdingManipulatorState; // Switching state to "holding manipulator state"
                }
                else if (hoveredEntity != null)
                {
                    SceneManager.SelectedEntity = hoveredEntity;
                }
            }



            // When pressing Right mouse button (without alt), switch to "Camera WASD moving state"
            if (Input.GetMouseButtonDown((int)MouseButton.RightMouse) && !pressingAlt && isMouseIn3DView)
            {
                _interfaceStateMachine.ActiveState = _cameraWasdMovingState;
            }

            // When pressing ald and Right mouse button, switch to "Camera WASD moving state"
            if (Input.GetMouseButtonDown((int)MouseButton.RightMouse) && pressingAlt && isMouseIn3DView)
            {
                _interfaceStateMachine.ActiveState = _cameraMouseZoomingState;
            }

            // When pressing ald and Right mouse button, switch to "Camera rotate around state"
            if (Input.GetMouseButtonDown((int)MouseButton.LeftMouse) && pressingAlt && isMouseIn3DView)
            {
                _interfaceStateMachine.ActiveState = _cameraRotateAroundState;
            }

            // When pressing Middle mouse button, switch to "Camera slide moving state"
            if (Input.GetMouseButtonDown((int)MouseButton.MiddleMouse) && isMouseIn3DView)
            {
                _interfaceStateMachine.ActiveState = _cameraSlideMovingState;
            }

            // When scrolling Mouse wheel, zoom in or out
            if (isMouseIn3DView)
                cameraController.ApplyZoom();



            // Switch Manipulator by shortcut
            // E -> Translate
            if (Input.GetKeyDown(KeyCode.E) && isMouseOverGameWindow)
            {
                GizmoManipulatorManager.instance.CurrentManipulator = GizmoManipulatorManager.Manipulator.Translate;
            }

            // R -> Rotate
            if (Input.GetKeyDown(KeyCode.R) && isMouseOverGameWindow)
            {
                GizmoManipulatorManager.instance.CurrentManipulator = GizmoManipulatorManager.Manipulator.Rotate;
            }

            // T -> Scale
            if (Input.GetKeyDown(KeyCode.T) && isMouseOverGameWindow)
            {
                GizmoManipulatorManager.instance.CurrentManipulator = GizmoManipulatorManager.Manipulator.Scale;
            }

            
            // Delete the Selected Entity
            if (Input.GetKeyDown(KeyCode.Delete) && isMouseOverGameWindow)
            {
                if (SceneManager.SelectedEntity != null)
                {
                    Destroy(SceneManager.SelectedEntity.gameObject);
                    SceneManager.SelectedEntity = null;
                }
            }


        };

        // This state is active, when the user is holding a Manipulator
        _holdingManipulatorState = new StateMachine.State("Holding manipulator state");
        _holdingManipulatorState.OnStateEnter = state =>
        {
            // set null, to indicate that this state was just entered
            _lastMousePosition = null;
        };
        _holdingManipulatorState.OnStateExit = state => GizmoRelationManager.onUpdate.Invoke();
        _holdingManipulatorState.OnStateUpdate = state =>
        {
            // Get the ray from the camera, where the mouse currently is
            var mouseRay = gizmoCamera.ViewportPointToRay(gizmoCamera.ScreenToViewportPoint(Input.mousePosition));

            // Get the 3D position on the Manipulator plane
            _activeManipulatorPlane.Raycast(mouseRay, out var distanceOnPlane);
            var mousePositionOnPlane = mouseRay.GetPoint(distanceOnPlane);

            //Debug.DrawLine(mousePositionOnPlane, _lastMousePosition.Value, Color.red, 10);
            _activeManipulatorPlane.DrawGizmo(_activeManipulator.GetOneRay());

            // if this is the first update, where the manipulator is hold, there is no movement to apply
            if (_lastMousePosition != null)
            {
                var globalMouseChange = mousePositionOnPlane - _lastMousePosition.Value;
                var localMouseChange = _activeManipulator.transform.InverseTransformDirection(globalMouseChange);
                var cameraSpaceMouseChange = gizmoCamera.transform.InverseTransformDirection(globalMouseChange);
                _activeManipulator.Change(globalMouseChange, localMouseChange, cameraSpaceMouseChange, gizmoCamera);
                //Debug.Log("Mouse change: "+globalMouseChange/Time.deltaTime);
            }

            // save the mouse position in a field. This is used to calculate the mouse movement in the next update
            _lastMousePosition = mousePositionOnPlane;

            // When the left mouse button is released, return to "Free mouse state"
            if (!Input.GetMouseButton((int)MouseButton.LeftMouse))
            {
                _interfaceStateMachine.ActiveState = _freeMouseState;
            }

            onTransformChange.Invoke();
        };

        // This state is active, when the user moves around using the WASD controlls
        _cameraWasdMovingState = new StateMachine.State("Camera WASD moving state");
        _cameraWasdMovingState.OnStateEnter = _ => cameraController.StartMovement();
        _cameraWasdMovingState.OnStateExit = _ => cameraController.EndMovement();
        _cameraWasdMovingState.OnStateUpdate = _ =>
        {
            cameraController.UpdateWasdMovement();

            // When releasing Right mouse button, switch to "Free mouse state"
            if (!Input.GetMouseButton((int)MouseButton.RightMouse))
            {
                _interfaceStateMachine.ActiveState = _freeMouseState;
            }
        };

        // This state is active, when the user zooms by holding alt + right mouse
        _cameraMouseZoomingState = new StateMachine.State("Camera mouse zooming state");
        _cameraMouseZoomingState.OnStateEnter = _ => cameraController.StartMovement();
        _cameraMouseZoomingState.OnStateExit = _ => cameraController.EndMovement();
        _cameraMouseZoomingState.OnStateUpdate = _ =>
        {
            cameraController.UpdateZoomMovement();

            // When releasing Right mouse button, switch to "Free mouse state"
            if (!Input.GetMouseButton((int)MouseButton.RightMouse))
            {
                _interfaceStateMachine.ActiveState = _freeMouseState;
            }
        };

        // This state is active, when the user rotates the camera around a point in the scene
        _cameraRotateAroundState = new StateMachine.State("Camera rotate around state");
        _cameraRotateAroundState.OnStateEnter = _ => cameraController.StartMovement();
        _cameraRotateAroundState.OnStateExit = _ => cameraController.EndMovement();
        _cameraRotateAroundState.OnStateUpdate = _ =>
        {
            if (_mouseInWorldPoint != null) cameraController.UpdateRotateAroundMovement(_mouseInWorldPoint.Value);

            // When releasing Left mouse button, switch to "Free mouse state"
            if (!Input.GetMouseButton((int)MouseButton.LeftMouse))
            {
                _interfaceStateMachine.ActiveState = _freeMouseState;
            }
        };


        // This state is active, when the user slides the camera around (Middle mouse button)
        _cameraSlideMovingState = new StateMachine.State("Camera slide moving state");
        _cameraSlideMovingState.OnStateEnter = _ => cameraController.StartMovement();
        _cameraSlideMovingState.OnStateExit = _ => cameraController.EndMovement();
        _cameraSlideMovingState.OnStateUpdate = _ =>
        {
            cameraController.UpdateSlideMovement();

            // When releasing Middle mouse button, switch to "Free mouse state"
            if (!Input.GetMouseButton((int)MouseButton.MiddleMouse))
            {
                _interfaceStateMachine.ActiveState = _freeMouseState;
            }
        };


        _interfaceStateMachine = new StateMachine(_freeMouseState);
    }

    void Update()
    {
        _interfaceStateMachine.Update();
    }
}

static class Util
{
    public static Vector3 randomVector3()
    {

        return new Vector3(Random.Range(-1.0f, 1.0f), Random.Range(-1.0f, 1.0f), Random.Range(-1.0f, 1.0f));
    }

    public static void DrawGizmo(this Plane p, Ray? startRay = null, int lines = 21, float spacing = 1)
    {
        startRay ??= new Ray(Vector3.zero, Vector3.up);

        var startPoint = startRay.Value.origin;

        // Put start point on plane
        var startPointOnPlane = p.ClosestPointOnPlane(startPoint);

        // Move start point to left border
        var horizontalRay = new Ray(startPointOnPlane, Vector3.Cross(p.normal, startRay.Value.direction));
        startPointOnPlane = horizontalRay.GetPoint(-(lines - 1) * spacing / 2);

        // Move start point to left bottom corner
        var verticalRay = new Ray(startPointOnPlane, Vector3.Cross(p.normal, horizontalRay.direction));

        // Draw Horizontal lines
        for (int i = 0; i < lines; i++)
        {
            var rayStartPoint = verticalRay.GetPoint((-(lines - 1) * spacing / 2) + i * spacing);
            Debug.DrawRay(rayStartPoint, horizontalRay.direction.normalized * (lines - 1) * spacing);
        }

        horizontalRay.origin = verticalRay.GetPoint(-(lines - 1) * spacing / 2);

        // Draw Vertical lines
        for (int i = 0; i < lines; i++)
        {
            var rayStartPoint = horizontalRay.GetPoint(/*(-(lines-1) * (-spacing/4))+*/i * spacing);
            Debug.DrawRay(rayStartPoint, verticalRay.direction.normalized * (lines - 1) * spacing);
        }

        //Debug.DrawRay(p.normal*-p.distance,Vector3.Cross(p.normal,Util.randomVector3()),Color.green,10);
    }
}