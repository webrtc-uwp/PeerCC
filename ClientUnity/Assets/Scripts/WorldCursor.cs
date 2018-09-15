using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class WorldCursor : MonoBehaviour
{
    private MeshRenderer meshRenderer;

    public Button ConnectButton;
    public Button CallButton;
    public Camera MainCamera;

    private bool connectButtonSelected = false;
    private bool callButtonSelected = false;

    void Start()
    {
        meshRenderer = this.gameObject.GetComponentInChildren<MeshRenderer>();
    }

    void Update()
    {
        var headPosition = MainCamera.transform.position;
        var gazeDirection = MainCamera.transform.forward;
        Debug.DrawRay(headPosition, gazeDirection * 10.0f, Color.red);
        RaycastHit hitInfo;
       
        if (Physics.Raycast(headPosition, gazeDirection, out hitInfo, 20.0f, Physics.DefaultRaycastLayers))
        {
            this.transform.position = new Vector3(hitInfo.point.x, hitInfo.point.y, -0.05f);
            this.transform.rotation = Quaternion.FromToRotation(Vector3.up, hitInfo.normal);
            meshRenderer.enabled = true;

            System.Diagnostics.Debug.WriteLine("WorldCursor - Raycast - objects hit");

            if (hitInfo.collider.gameObject.GetComponent<Button>() == ConnectButton)
            {
                if (!connectButtonSelected)
                {
                    connectButtonSelected = true;
                    ColorBlock colorBlock = ConnectButton.colors;
                    colorBlock.normalColor = Color.red;
                    ConnectButton.colors = colorBlock;
                }
            }
            else if (hitInfo.collider.gameObject.GetComponent<Button>() == CallButton)
            {
                if (!callButtonSelected)
                {
                    callButtonSelected = true;
                    ColorBlock colorBlock = CallButton.colors;
                    colorBlock.normalColor = Color.red;
                    CallButton.colors = colorBlock;
                }
            }
            else if (connectButtonSelected || callButtonSelected)
            {
                ResetButtons();
            }
        }
        else if (connectButtonSelected || callButtonSelected)
        {
            meshRenderer.enabled = false;

            ResetButtons();
        }
    }

    private void ResetButtons()
    {
        connectButtonSelected = false;
        ColorBlock colorBlock = ConnectButton.colors;
        colorBlock.normalColor = Color.white;
        ConnectButton.colors = colorBlock;

        callButtonSelected = false;
        colorBlock = CallButton.colors;
        colorBlock.normalColor = Color.white;
        CallButton.colors = colorBlock;
    }
}