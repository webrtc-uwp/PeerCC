using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.XR.WSA.Input;

public class GazeGestureManager : MonoBehaviour
{
    public static GazeGestureManager Instance { get; private set; }

    public GameObject FocusedObject { get; private set; }

    GestureRecognizer recognizer;

    public Button ConnectButton;
    public Button CallButton;

    void Start()
    {
        Instance = this;

        recognizer = new GestureRecognizer();
        recognizer.Tapped += (args) =>
        {
            if (FocusedObject != null)
            {
                if (FocusedObject.GetComponent<Button>() == ConnectButton)
                {
                    ControlScript.Instance.OnConnectClick();
                }
                else if (FocusedObject.GetComponent<Button>() == CallButton)
                {
                    ControlScript.Instance.OnCallClick();
                }
            }
        };
        recognizer.StartCapturingGestures();
    }

    void Update()
    {
        GameObject oldFocusObject = FocusedObject;

        var headPosition = Camera.main.transform.position;
        var gazeDirection = Camera.main.transform.forward;

        RaycastHit hitInfo;
        if (Physics.Raycast(headPosition, gazeDirection, out hitInfo, 20.0f, Physics.DefaultRaycastLayers))
        {
            FocusedObject = hitInfo.collider.gameObject;

            if (hitInfo.collider.gameObject.GetComponent<Button>() == ConnectButton)
            {
                FocusedObject = hitInfo.collider.gameObject;
            }
            else if (hitInfo.collider.gameObject.GetComponent<Button>() == CallButton)
            {
                FocusedObject = hitInfo.collider.gameObject;
            }
            else
            {
                FocusedObject = null;
            }
        }
        else
        {
            FocusedObject = null;
        }

        if (FocusedObject != oldFocusObject)
        {
            recognizer.CancelGestures();
            recognizer.StartCapturingGestures();
        }
    }
}