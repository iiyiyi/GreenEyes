﻿using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;
using static EnvironmentData;
using System.Threading;
using System.Collections;

public class SceneOrganiser : MonoBehaviour
{
    /// <summary>
    /// Allows this class to behave like a singleton
    /// </summary>
    public static SceneOrganiser Instance;

    /// <summary>
    /// The cursor object attached to the Main Camera
    /// </summary>
    internal GameObject cursor;

    /// <summary>
    /// The label used to display the analysis on the objects in the real world
    /// </summary>
    public GameObject label;

    [SerializeField]
    private GameObject dataPanelPrefab;
    [SerializeField]
    private GameObject loadingIconPrefab;

    /// <summary>
    /// Reference to the last Label positioned
    /// </summary>
    internal Transform lastLabelPlaced;

    /// <summary>
    /// Reference to the last Label positioned
    /// </summary>
    internal TextMesh lastLabelPlacedText;

    /// <summary>
    /// Current threshold accepted for displaying the label
    /// Reduce this value to display the recognition more often
    /// </summary>
    internal float probabilityThreshold = 0.3f;

    /// <summary>
    /// The quad object hosting the imposed image captured
    /// </summary>
    private GameObject quad;

    /// <summary>
    /// Renderer of the quad object
    /// </summary>
    internal Renderer quadRenderer;

    private Vector3 latestDataPanelPosition;
    private Quaternion latestDataPanelRotation;

    private Vector3 captureTimeHeadPosition;
    private LayerMask captureTimeRaycastMask;

    private GameObject lastLoadingIcon;

    private EnvironmentData envData;

    // Start is called before the first frame update

    /// <summary>
    /// Called on initialization
    /// </summary>
    private void Awake()
    {
        // Use this class instance as singleton
        Instance = this;

        // Add the ImageCapture class to this Gameobject
        gameObject.AddComponent<ImageCapture>();

        // Add the CustomVisionAnalyser class to this Gameobject
        gameObject.AddComponent<CustomVisionAnalyser>();

        // Add the CustomVisionObjects class to this Gameobject
        gameObject.AddComponent<CustomVisionObjects>();

        envData = new EnvironmentData();
    }

    /// <summary>
    /// Instantiate a Label in the appropriate location relative to the Main Camera.
    /// </summary>
    public void PlaceAnalysisLabel()
    {
        lastLabelPlaced = Instantiate(label.transform, cursor.transform.position, transform.rotation);
        lastLabelPlacedText = lastLabelPlaced.GetComponent<TextMesh>();
        //lastLabelPlacedText.text = "Loading...";
        lastLabelPlaced.transform.localScale = new Vector3(0.005f, 0.005f, 0.005f);

        lastLoadingIcon = MakeLoadingIcon(cursor.transform.position, transform.rotation);

        latestDataPanelPosition = cursor.transform.position;
        latestDataPanelRotation = transform.rotation;

        // Create a GameObject to which the texture can be applied
        quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quadRenderer = quad.GetComponent<Renderer>() as Renderer;
        Material m = new Material(Shader.Find("Legacy Shaders/Transparent/Diffuse"));
        quadRenderer.material = m;

        // Here you can set the transparency of the quad. Useful for debugging
        float transparency = 0.0f;
        quadRenderer.material.color = new Color(1, 1, 1, transparency);

        // Set the position and scale of the quad depending on user position
        quad.transform.parent = transform;
        quad.transform.rotation = transform.rotation;

        // The quad is positioned slightly forward in font of the user
        quad.transform.localPosition = new Vector3(0.0f, 0.0f, 3.0f);

        // The quad scale as been set with the following value following experimentation,  
        // to allow the image on the quad to be as precisely imposed to the real world as possible
        quad.transform.localScale = new Vector3(3f, 1.65f, 1f);
        quad.transform.parent = null;

        captureTimeHeadPosition = Camera.main.transform.position;
        captureTimeRaycastMask = SpatialMapping.PhysicsRaycastMask;
    }

    /// <summary>
    /// Set the Tags as Text of the last label created. 
    /// </summary>
    public void FinaliseLabel(AnalysisRootObject analysisObject)
    {
        if (analysisObject != null && analysisObject.predictions != null)
        {
            lastLabelPlacedText = lastLabelPlaced.GetComponent<TextMesh>();
            // Sort the predictions to locate the highest one
            List<Prediction> sortedPredictions = new List<Prediction>();
            sortedPredictions = analysisObject.predictions.OrderBy(p => p.probability).ToList();

            //TOO remove below
            /*            quadRenderer = quad.GetComponent<Renderer>() as Renderer;
                        Bounds quadBounds = quadRenderer.bounds;
                        lastLabelPlaced.transform.parent = quad.transform;
                        // Vector3 labelPosition = new Vector3((float)-quadBounds.size.normalized.x/2, (float)-quadBounds.size.normalized.y/2, 0);
                        Vector3 labelPosition = new Vector3(0, 0, 0);
                        lastLabelPlaced.transform.localPosition = labelPosition;
                        lastLabelPlacedText.text = "";//bestPrediction.tagName;
                        Debug.Log("Repositioning Label");
                        Vector3 headPosition = Camera.main.transform.position;
                        RaycastHit objHitInfo;
                        Vector3 objDirection = lastLabelPlaced.position;

                        if (Physics.Raycast(captureTimeHeadPosition, objDirection, out objHitInfo, 30.0f, captureTimeRaycastMask))
                        {
                            lastLabelPlaced.position = objHitInfo.point;
                            latestDataPanelPosition = objHitInfo.point;
                        }
                        ObjectData data = envData.getObjectData("water bottle");
                        data.probability = 0.90;
                        StartCoroutine(makeDataPanel(data, latestDataPanelPosition, latestDataPanelRotation));
                        IEnumerator coroutine = ResetLoadingUI(lastLabelPlacedText, lastLoadingIcon, 5.0f);
                        StartCoroutine(coroutine); */
            //TODO uncomment below
            Prediction bestPrediction = new Prediction();

            if (sortedPredictions.Count > 0)
            {
                bestPrediction = sortedPredictions[sortedPredictions.Count - 1];
            }
            else
            {
                bestPrediction.probability = 0;
            }

            if (bestPrediction.probability > probabilityThreshold)
            {
                quadRenderer = quad.GetComponent<Renderer>() as Renderer;
                Bounds quadBounds = quadRenderer.bounds;

                // Position the label as close as possible to the Bounding Box of the prediction 
                // At this point it will not consider depth
                lastLabelPlaced.transform.parent = quad.transform;
                Vector3 labelPosition = CalculateBoundingBoxPosition(quadBounds, bestPrediction.boundingBox);
                lastLabelPlaced.transform.localPosition = labelPosition;

                // Set the tag text
                lastLabelPlacedText.text = "";//bestPrediction.tagName;


                // Cast a ray from the user's head to the currently placed label, it should hit the object detected by the Service.
                // At that point it will reposition the label where the ray HL sensor collides with the object,
                // (using the HL spatial tracking)
                Debug.Log("Repositioning Label");
                Vector3 headPosition = Camera.main.transform.position;
                RaycastHit objHitInfo;
                Vector3 objDirection = lastLabelPlaced.position;

                if (Physics.Raycast(captureTimeHeadPosition, objDirection, out objHitInfo, 30.0f, captureTimeRaycastMask))
                {
                    lastLabelPlaced.position = objHitInfo.point;
                    latestDataPanelPosition = objHitInfo.point;
                }
                Debug.Log("Success");

                ObjectData data = envData.getObjectData(bestPrediction.tagName);
                data.probability = bestPrediction.probability;
                StartCoroutine(MakeDataPanel(data, latestDataPanelPosition, latestDataPanelRotation));
            }
            else
            {

                LoadingRotation loadingRotation = lastLoadingIcon.GetComponent<LoadingRotation>();
                loadingRotation.Failed();
            }

            IEnumerator coroutine = ResetLoadingUI(lastLabelPlacedText, lastLoadingIcon, 5.0f);
            StartCoroutine(coroutine);
        }

        // Reset the color of the cursor
        cursor.GetComponent<Renderer>().material.color = Color.yellow;

        // Stop the analysis process
        ImageCapture.Instance.ResetImageCapture();
    }

    private IEnumerator ResetLoadingUI(TextMesh label, GameObject lastLoading, float waitTime)
    {
        yield return new WaitForSeconds(waitTime);
        label.text = "";
        Destroy(lastLoading);
    }

    /// <summary>
    /// This method hosts a series of calculations to determine the position 
    /// of the Bounding Box on the quad created in the real world
    /// by using the Bounding Box received back alongside the Best Prediction
    /// </summary>
    public Vector3 CalculateBoundingBoxPosition(Bounds b, BoundingBox boundingBox)
    {
        Debug.Log($"BB: left {boundingBox.left}, top {boundingBox.top}, width {boundingBox.width}, height {boundingBox.height}");

        double centerFromLeft = boundingBox.left + (boundingBox.width / 2);
        double centerFromTop = boundingBox.top + (boundingBox.height / 2);
        Debug.Log($"BB CenterFromLeft {centerFromLeft}, CenterFromTop {centerFromTop}");

        double quadWidth = b.size.normalized.x;
        double quadHeight = b.size.normalized.y;
        Debug.Log($"Quad Width {b.size.normalized.x}, Quad Height {b.size.normalized.y}");

        double normalisedPos_X = (quadWidth * centerFromLeft) - (quadWidth / 2);
        double normalisedPos_Y = (quadHeight * centerFromTop) - (quadHeight / 2);

        return new Vector3((float)normalisedPos_X, (float)normalisedPos_Y, 0);
    }

    private GameObject MakeLoadingIcon(Vector3 position, Quaternion rotation)
    {
        GameObject loadingIcon = GameObject.Instantiate(loadingIconPrefab, position, rotation);
        loadingIcon.transform.localPosition = position;

        return loadingIcon;
    }

    private IEnumerator MakeDataPanel(ObjectData data, Vector3 position, Quaternion rotation) {
        yield return new WaitForSeconds(0);

        Debug.Log("make a data panel");

        GameObject dataPanel = GameObject.Instantiate(dataPanelPrefab, position, rotation);
        dataPanel.transform.localPosition = position;

        Transform window = dataPanel.transform.Find("SF Window");

        Transform titleLbl = window.Find("Title");
        TextMeshProUGUI titleMesh = titleLbl.GetComponent<TextMeshProUGUI>();
        titleMesh.text = data.name;

        Transform icons = window.Find("Icons");

        PopulateGrid populateGrid = icons.GetComponent<PopulateGrid>();
        populateGrid.scores[0] = data.carbonScore;
        populateGrid.scores[1] = data.waterScore;
        populateGrid.scores[2] = data.landScore;
        populateGrid.Populate();
    }
}