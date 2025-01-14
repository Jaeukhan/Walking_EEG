using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Redirection;
using System;
using System.Text;
using System.IO;
using Vrwave;
using System.Media;
using Tobii.XR;
using Tobii.G2OM;

public class RedirectionManager : MonoBehaviour {

    public enum MovementController { Keyboard, AutoPilot, Tracker };

    [Tooltip("Select if you wish to run simulation from commandline in Unity batchmode.")]
    [HideInInspector]
    public bool runInTestMode = true;

    [Tooltip("How user movement is controlled.")]
    public MovementController MOVEMENT_CONTROLLER;
    
    [Tooltip("Maximum translation gain applied")]
    [Range(0, 5)]
    public float MAX_TRANS_GAIN = 0.26F;
    
    [Tooltip("Minimum translation gain applied")]
    [Range(-0.99F, 0)]
    public float MIN_TRANS_GAIN = -0.14F;
    
    [Tooltip("Maximum rotation gain applied")]
    [Range(0, 5)]
    public float MAX_ROT_GAIN = 1.25f;// 0.49F;
    
    [Tooltip("Minimum rotation gain applied")]
    [Range(-0.2F, 1.0F)]
    public float MIN_ROT_GAIN = 0.67F;// -0.2F;

    [Tooltip("Radius applied by curvature gain")]
    [Range(1, 23)]
    public float CURVATURE_RADIUS = 7.5F;

    [Tooltip("rotation gain applied")]

    [Range(0.5F, 2.0F)]
    public float ROT_GAIN = 0.67F;// -0.2F;
    // [Range(0.5F, 2.0F)]
    //public float change_rotgain = 0.67F;// -0.2F;

    //public int changeorder = 7;



    [Tooltip("The game object that is being physically tracked (probably user's head)")]
    public Transform headTransform;

    [Tooltip("Use simulated framerate in auto-pilot mode")]
    public bool useManualTime = false;

    [Tooltip("Target simulated framerate in auto-pilot mode")]
    public float targetFPS = 60;

    public class Global
    {
        public static int repetition = 0;
        public static int count = 0;
        public static bool isRunning = false;
        public static List<int> list;
        public static float leftPosition, rightPosition;
        public static string selection = "null";
    }

    public AudioSource beep;

    public static bool usingreset = false;

    [HideInInspector]
    public Transform body;
    [HideInInspector]
    public Transform duplicatedBody;
    [HideInInspector]
    public Transform trackedSpace;
    [HideInInspector]
    public Transform simulatedHead;

    [HideInInspector]
    public Redirector redirector;
    [HideInInspector]
    public Resetter resetter;
    [HideInInspector]
    public ResetTrigger resetTrigger;
    [HideInInspector]
    public TrailDrawer trailDrawer;
    [HideInInspector]
    public SimulationManager simulationManager;
    [HideInInspector]
    public SimulatedWalker simulatedWalker;
    [HideInInspector]
    public KeyboardController keyboardController;
    [HideInInspector]
    public SnapshotGenerator snapshotGenerator;
    [HideInInspector]
    public StatisticsLogger statisticsLogger;
    [HideInInspector]
    public HeadFollower bodyHeadFollower;

    [HideInInspector]
    public Vector3 currPos, currPosReal, prevPos, prevPosReal;
    [HideInInspector]
    public Vector3 currDir, currDirReal, prevDir, prevDirReal;
    [HideInInspector]
    public Vector3 deltaPos;
    [HideInInspector]
    public float deltaDir;
    [HideInInspector]
    public Transform targetWaypoint;

    public _VisualizerManager visualizerManager;
    public int seed = 42;
    public bool gainrand = false;

    [HideInInspector]
    public bool inReset = false;

    [HideInInspector]
    public string startTimeOfProgram;
    private float simulatedTime = 0;

    [HideInInspector]
    public GeometryInfo geometryInfo;
    
    [HideInInspector]
    public bool tilingMode = false;

    [HideInInspector]
    public bool controllerTriggered = false;

    [HideInInspector]
    public string roomTypeName;
    [HideInInspector]
    public float startTime; 
    [HideInInspector]
    public int baselinesecond = 0;
    [HideInInspector]
    public float timefactor = 1.0f;

    public GeometryInfo.SpaceShape spaceShape;

    public ActionTest actionTest;
    private bool initialbase = false;

    private int oneTime = -3;
    private int previousOneTime = 0;
    private int previousOneTime2 = 0;
    private bool breaktime = false;
    private float[] randrot;
    private bool beepbool = true;
    private bool startexp = true;
    
    private StreamWriter sw;
  

    //[HideInInspector]
    //bool needChange1 = false;
    //[HideInInspector]
    //bool needChange2 = false;
    //[HideInInspector]
    //bool needChange3 = false;
    //[HideInInspector]
    //bool needChange4 = false;

    // [HideInInspector]
    // bool testBool = true;
    [HideInInspector]
    List<int> randomized;
    
    [HideInInspector]
    int initialIndex;

    [HideInInspector]
    CylinderCollisionTrigger cylinderCollisionTrigger;

    void Awake()
    {
        startTimeOfProgram = System.DateTime.Now.ToString("yyyy MM dd HH:mm:ss");

        setRoomType();

        getGeometryInfo();

        GetBody();
        GetDuplicatedBody();
        GetTrackedSpace();
        GetSimulatedHead();

        GetSimulationManager();
        SetReferenceForSimulationManager();
        simulationManager.Initialize();
        

        GetRedirector();
        GetResetter();
        GetResetTrigger();
        GetTrailDrawer();
        
        GetSimulatedWalker();
        GetKeyboardController();
        GetSnapshotGenerator();
        GetStatisticsLogger();
        GetBodyHeadFollower();
        SetReferenceForRedirector();
        SetReferenceForResetter();
        SetReferenceForResetTrigger();
        SetBodyReferenceForResetTrigger();
        SetReferenceForTrailDrawer();

        SetReferenceForSimulatedWalker();
        SetReferenceForKeyboardController();
        SetReferenceForSnapshotGenerator();
        SetReferenceForStatisticsLogger();
        SetReferenceForBodyHeadFollower();

        sw = new StreamWriter("data.csv", false);

        // The rule is to have RedirectionManager call all "Awake"-like functions that rely on RedirectionManager as an "Initialize" call.
        if(!simulationManager.runInSimulationMode) resetTrigger.Initialize();
        // Resetter needs ResetTrigger to be initialized before initializing itself
        if (resetter != null)
            resetter.Initialize();

        if (simulationManager.runInSimulationMode)
        {
            MOVEMENT_CONTROLLER = MovementController.AutoPilot;
            headTransform = simulatedHead;
        }
        else
        {
            MOVEMENT_CONTROLLER = MovementController.Tracker;
        }

        setDoorSetting();
        //setCylinderCollisionTrigger();

    }

	// Use this for initialization
	void Start () {
        simulatedTime = 0;
        UpdatePreviousUserState();
        if (gainrand == true)
        {
            UnityEngine.Random.InitState(seed);
            float[] ran = new float[2] {1.0f, 1.6f};
            randrot = visualizerManager.Shuffle(ran);
            ROT_GAIN = randrot[0];
            Debug.Log(ROT_GAIN);
        }


        List<int> randomOrder = new List<int>() {0, 1, 2, 3};
        System.Random rnd = new System.Random();
        System.Linq.IOrderedEnumerable<int> randomizedNums = randomOrder.OrderBy(item => rnd.Next());
        randomized = new List<int>();
        foreach (int i in randomizedNums)
        {
            randomized.Add(i);
        }

        for(int i = 0; i<4; i++)
        {
            if(randomized[i] == 0)
            {
                initialIndex = i;
            }
        }

	}
    void StartTimeSet()
    {
        if(startexp)
        {
            startTime = Time.time;
        }
        startexp = false;
    }
	
    void BeepSoundExp()
    {
        if(_VisualizerManager.savestate)
        {
            StartTimeSet();
            if ((int)((Time.time - startTime)/ timefactor) - baselinesecond > 0)
            {   
                if (baselinesecond == 0)
                {
                    if (beepbool == true)
                    {
                        beep.Play(); //4초부터 시작함. 6 11 16 21
                        Debug.Log("도세요");
                    }
                }
                if (baselinesecond % 5 == 0 && baselinesecond != 0)
                {
                        if (beepbool == true)
                        {
                            beep.Play(); //4초부터 시작함. 6 11 16 21
                            Debug.Log("도세요");
                        }
                    oneTime++;
                }
                if (baselinesecond >= 1f && baselinesecond < 3f)
                {
                    Debug.Log("준비하세요");
                }

            }

            else if ((Time.time -startTime)/ timefactor >= 181)
            {
                visualizerManager.exitstate = true;
                beepbool = false;
                if (actionTest.GetoddballCount())
                {
                    Debug.Log("가상환경이 적게돌아감");
                }
            }
            Debug.Log((Time.time - startTime) / timefactor);
            baselinesecond = (int)((Time.time-startTime) / timefactor);
        }
    }

    void chairroate()
    {
        if (_VisualizerManager.savestate)
        {
            StartTimeSet();
            // if ((int)((Time.time - startTime) / timefactor) - baselinesecond > 0)
            // {
            //     if (baselinesecond == 2)
            //     {
            //         if (beepbool == true)
            //         {
            //             beep.Play();
            //             Debug.Log("도세요");
            //         }
            //     }
            // }
            // //90도 돌아가면 stop
            // if (headTransform.transform.rotation.y >= 0.9)
            // {
            //     visualizerManager.exitstate = true;
            //     beepbool = false;
            //     if (actionTest.GetoddballCount())
            //     {
            //         Debug.Log("가상환경이 적게돌아감");
            //     }
            // }

            if ((Time.time -startTime)/ timefactor >= 8)
            {
                ROT_GAIN = 1.1f;
            }
            
            if ((Time.time -startTime)/ timefactor >= 13)
            {
                ROT_GAIN = 1.2f;
            }
            
            if ((Time.time -startTime)/ timefactor >= 18)
            {
                ROT_GAIN = 1.3f;
            }

                        if ((Time.time -startTime)/ timefactor >= 23)
            {
                ROT_GAIN = 1.4f;
            }
                        if ((Time.time -startTime)/ timefactor >= 28)
            {
                ROT_GAIN = 1.5f;
            }






            if ((Time.time -startTime)/ timefactor >= 33)
            {
                beep.Play();
                visualizerManager.exitstate = true;
                beepbool = false;
                if (actionTest.GetoddballCount())
                {
                    Debug.Log("가상환경이 적게돌아감");
                }
            }

            baselinesecond = (int)((Time.time - startTime) / timefactor);
        }
        
    }

    void FixedUpdate()
    {

        //BeepSoundExp();
        chairroate();

        UpdateCurrentUserState();
        CalculateStateChanges();

        // BACK UP IN CASE UNITY TRIGGERS FAILED TO COMMUNICATE RESET (Can happen in high speed simulations)
        //if (resetter != null && !inReset && resetter.IsUserOutOfBounds() && !tilingMode)
        if (resetter != null && !inReset && false && !tilingMode)
        {
            Debug.LogWarning("Reset Aid Helped!");
            OnResetTrigger();
        }
        else if (resetter != null && !inReset && resetter.ControllerTriggered() && tilingMode)
        {
            //Debug.Log(controllerTriggered);
            this.controllerTriggered = false;
            Debug.LogWarning("Reset Aid Helped!");
            OnResetTrigger();
        }

        if (inReset)
        {
            if (resetter != null)
            {
                resetter.ApplyResetting();
            }
        }
        else
        {
            if (redirector != null)
            {
                redirector.ApplyRedirection();
            }
        }

        statisticsLogger.UpdateStats();

        UpdatePreviousUserState();

        UpdateBodyPose();
        UpdateDuplicatedBodyPose();
    }


    public float GetDeltaTime()
    {
        if (useManualTime)
            return 1.0f / targetFPS;
        else
            return Time.deltaTime; // APF 정상동작: project settin g: fixed timestep: 0.02, time scale: 0.8
    }

    public float GetTime()
    {
        if (useManualTime)
            return simulatedTime;
        else
            return Time.time;
    }
    void LateUpdate()
    {
        if (_VisualizerManager.savestate)
        {
            simulatedTime += 1.0f / targetFPS;
            SaveData(ROT_GAIN, visualizerManager.exporder);
        }

    }

    public void SaveData(float curvature, string ordername) //, int repetition, int count
    {
        var eyeTrackingData = TobiiXR.GetEyeTrackingData(TobiiXR_TrackingSpace.World);

        // Check if gaze ray is valid
        //if (eyeTrackingData.GazeRay.IsValid)
        //{
        // The origin of the gaze ray is a 3D point
        var rayOrigin = eyeTrackingData.GazeRay.Origin;

        // The direction of the gaze ray is a normalized direction vector
        var rayDirection = eyeTrackingData.GazeRay.Direction;
        //}

        
        // The EyeBlinking bool is true when the eye is closed
        //var isLeftEyeBlinking = eyeTrackingData.IsLeftEyeBlinking;
        //var isRightEyeBlinking = eyeTrackingData.IsRightEyeBlinking;

        // Using gaze direction in local space makes it easier to apply a local rotation
        // to your virtual eye balls.


        float gazeValid = Convert.ToSingle(eyeTrackingData.GazeRay.IsValid);
        float convergenceValid = Convert.ToSingle(eyeTrackingData.ConvergenceDistanceIsValid);
        float leftBlink = Convert.ToSingle(eyeTrackingData.IsLeftEyeBlinking);
        float rightBlink = Convert.ToSingle(eyeTrackingData.IsRightEyeBlinking);



        /*
        if (eyeTrackingData.GazeRay.IsValid == true)
            gazeValid = 1.0f;
        else
            gazeValid = 0.0f;

        if (eyeTrackingData.IsLeftEyeBlinking == true)
            leftBlink = 1.0f;
        else
            leftBlink = 0.0f;

        if (eyeTrackingData.IsRightEyeBlinking == true)
            rightBlink = 1.0f;
        else*
            rightBlink = 0.0f;


        if (eyeTrackingData.ConvergenceDistanceIsValid == true)
            convergenceValid = 1.0f;
        else
            convergenceValid = 0.0f;
        */

        // For social use cases, data in local space may be easier to work with
        var eyeTrackingDataLocal = TobiiXR.GetEyeTrackingData(TobiiXR_TrackingSpace.Local);

        var rayDirectionLocal = eyeTrackingDataLocal.GazeRay.Direction;
        var rayOriginLocal = eyeTrackingDataLocal.GazeRay.Origin;



        // float[] info = new float[] { curvature, Convert.ToSingle(repetition), Convert.ToSingle(count) };
        float info = curvature;
        float[] eye = new float[] { eyeTrackingData.Timestamp, eyeTrackingData.ConvergenceDistance, gazeValid, convergenceValid, rayOrigin.x, rayOrigin.y, rayOrigin.z, rayDirection.x, rayDirection.y, rayDirection.z, leftBlink, rightBlink, rayOriginLocal.x, rayOriginLocal.y, rayOriginLocal.z, rayDirectionLocal.x, rayDirectionLocal.y, rayDirectionLocal.z, };


        float[] output = new float[] { simulatedTime, currPos.x, currPos.y, currPos.z, currPosReal.x, currPosReal.y, currPosReal.z, currDir.x, currDir.y, currDir.z, currDirReal.x, currDirReal.y, currDirReal.z };
        //sw.Write(currPos[0].xcurrPos[1],currPos[);
        string delimiter = ",";
        StringBuilder sb = new StringBuilder();


        sb.Append(info.ToString() + delimiter);
        sb.Append(ordername + delimiter);
        // for(int index = 0; index < 1; index++)
        // {
        //     sb.Append(info[index].ToString() + delimiter);
        // }

        for (int index = 0; index < 18; index++)
        {
            sb.Append(eye[index].ToString() + delimiter);
        }

        for (int index = 0; index < 13; index++)
        {
            sb.Append(output[index].ToString() + delimiter);
        }

        sb.Append(Global.selection);


        sw.WriteLine(sb.ToString());

    }

    void UpdateDuplicatedBodyPose()
    {
        duplicatedBody.position = currPosReal + new Vector3(-200f, 0f, 200f);
        duplicatedBody.rotation = Quaternion.LookRotation(Utilities.FlattenedDir3D(currDirReal.normalized), Vector3.up);
    }

    void UpdateBodyPose()
    {
        body.position = Utilities.FlattenedPos3D(headTransform.position);
        body.rotation = Quaternion.LookRotation(Utilities.FlattenedDir3D(headTransform.forward), Vector3.up);
    }

    void SetReferenceForRedirector()
    {
        if (redirector != null)
            redirector.redirectionManager = this;
    }

    void SetReferenceForResetter()
    {
        if (resetter != null)
            resetter.redirectionManager = this;
    }

    void SetReferenceForResetTrigger()
    {
        if (resetTrigger != null)
            resetTrigger.redirectionManager = this;
    }

    void SetBodyReferenceForResetTrigger()
    {
        if (resetTrigger != null && body != null)
        {
            // NOTE: This requires that getBody gets called before this
            resetTrigger.bodyCollider = body.GetComponentInChildren<CapsuleCollider>();
            // Debug.Log("resetTrigger.bodyCollider.radius: "+resetTrigger.bodyCollider.radius);
        }
    }

    void SetReferenceForTrailDrawer()
    {
        if (trailDrawer != null)
        {
            trailDrawer.redirectionManager = this;
        }
    }

    void SetReferenceForSimulationManager()
    {
        if (simulationManager != null)
        {
            simulationManager.redirectionManager = this;
        }
    }

    void SetReferenceForSimulatedWalker()
    {
        if (simulatedWalker != null)
        {
            simulatedWalker.redirectionManager = this;
        }
    }

    void SetReferenceForKeyboardController()
    {
        if (keyboardController != null)
        {
            keyboardController.redirectionManager = this;
        }
    }

    void SetReferenceForSnapshotGenerator()
    {
        if (snapshotGenerator != null)
        {
            snapshotGenerator.redirectionManager = this;
        }
    }

    void SetReferenceForStatisticsLogger()
    {
        if (statisticsLogger != null)
        {
            statisticsLogger.redirectionManager = this;
        }
    }

    void SetReferenceForBodyHeadFollower()
    {
        if (bodyHeadFollower != null)
        {
            bodyHeadFollower.redirectionManager = this;
        }
    }

    void GetRedirector()
    {
        redirector = this.gameObject.GetComponent<Redirector>();
        if (redirector == null)
            this.gameObject.AddComponent<NullRedirector>();
        redirector = this.gameObject.GetComponent<Redirector>();
    }

    void GetResetter()
    {
        resetter = this.gameObject.GetComponent<Resetter>();
        if (resetter == null)
            this.gameObject.AddComponent<NullResetter>();
        resetter = this.gameObject.GetComponent<Resetter>();
    }

    void GetResetTrigger()
    {
        resetTrigger = this.gameObject.GetComponentInChildren<ResetTrigger>();
    }

    void GetTrailDrawer()
    {
        trailDrawer = this.gameObject.GetComponent<TrailDrawer>();
    }

    void GetSimulationManager()
    {
        simulationManager = this.gameObject.GetComponent<SimulationManager>();
    }

    void GetSimulatedWalker()
    {
        simulatedWalker = simulatedHead.GetComponent<SimulatedWalker>();
    }

    void GetKeyboardController()
    {
        keyboardController = simulatedHead.GetComponent<KeyboardController>();
    }

    void GetSnapshotGenerator()
    {
        snapshotGenerator = this.gameObject.GetComponent<SnapshotGenerator>();
    }

    void GetStatisticsLogger()
    {
        statisticsLogger = this.gameObject.GetComponent<StatisticsLogger>();
    }

    void GetBodyHeadFollower()
    {
        bodyHeadFollower = body.GetComponent<HeadFollower>();
    }

    void GetBody()
    {
        body = transform.Find("Body");
    }

    void GetDuplicatedBody()
    {
        duplicatedBody = GameObject.Find("DuplicatedBody").transform;
    }

    void GetTrackedSpace()
    {
        trackedSpace = transform.Find("Tracked Space");
    }

    void GetSimulatedHead()
    {
        simulatedHead = transform.Find("Simulated User").Find("Head");
    }

    void GetTargetWaypoint()
    {
        targetWaypoint = transform.Find("Target Waypoint").gameObject.transform;
    }

    void UpdateCurrentUserState()
    {
        currPos = Utilities.FlattenedPos3D(headTransform.position);
        currPosReal = Utilities.GetRelativePosition(currPos, this.transform);
        currDir = Utilities.FlattenedDir3D(headTransform.forward);
        currDirReal = Utilities.FlattenedDir3D(Utilities.GetRelativeDirection(currDir, this.transform));
        trailDrawer.AddRealDir();
    }
    

    void UpdatePreviousUserState()
    {
        prevPos = Utilities.FlattenedPos3D(headTransform.position);
        prevPosReal = Utilities.GetRelativePosition(prevPos, this.transform);
        prevDir = Utilities.FlattenedDir3D(headTransform.forward);
        prevDirReal = Utilities.FlattenedDir3D(Utilities.GetRelativeDirection(prevDir, this.transform));
    }

    void CalculateStateChanges()
    {
        deltaPos = currPos - prevPos;
        deltaDir = Utilities.GetSignedAngle(prevDir, currDir);
    }

    public void OnResetTrigger()
    {
        if (inReset)
            return;
        //print("NOT IN RESET");
        //print("Is Resetter Null? " + (resetter == null));
        //Debug.Log("resetter.IsResetRequired(): "+resetter.IsResetRequired());
        if (resetter != null && resetter.IsResetRequired() && !tilingMode)
        {
            //print("RESET WAS REQUIRED");
            Debug.Log("reset"+Time.time);
            resetter.InitializeReset();
            usingreset = true;
            inReset = true;
            trailDrawer.AddResetTime();
            // visualizerManager.EEG2Csv();

        }
        else if(tilingMode) // 조개줍기 등의 시나리오 필요, 사운드 필요, Drawer 보이기 안보이기 필요
        {
            resetter.InitializeReset();
            inReset = true;
            usingreset = true;
            trailDrawer.AddResetTime();
        }
    }

    public void OnResetEnd()
    {
        //print("RESET END");
        resetter.FinalizeReset();
        inReset = false;
        usingreset = false;
    }

    public void RemoveRedirector()
    {
        this.redirector = this.gameObject.GetComponent<Redirector>();
        if (this.redirector != null)
            Destroy(redirector);
        redirector = null;
    }

    public void UpdateRedirector(System.Type redirectorType)
    {
        RemoveRedirector();
        this.redirector = (Redirector) this.gameObject.AddComponent(redirectorType);
        //this.redirector = this.gameObject.GetComponent<Redirector>();
        SetReferenceForRedirector();
    }

    public void RemoveResetter()
    {
        this.resetter = this.gameObject.GetComponent<Resetter>();
        if (this.resetter != null)
            Destroy(resetter);
        resetter = null;
    }

    public void UpdateResetter(System.Type resetterType)
    {
        RemoveResetter();
        if (resetterType != null)
        {
            this.resetter = (Resetter) this.gameObject.AddComponent(resetterType);
            //this.resetter = this.gameObject.GetComponent<Resetter>();
            SetReferenceForResetter();
            if (this.resetter != null)
                this.resetter.Initialize();
        }
    }

    public void UpdateTrackedSpaceDimensions(float x, float z)
    {
        trackedSpace.localScale = new Vector3(x, 1, z);
        resetTrigger.Initialize();
        if (this.resetter != null)
            this.resetter.Initialize();
    }

    public void UpdateTrackedSpaceLocation(float x, float z)
    {
        trackedSpace.position = new Vector3(x, 1, z);
    }

    public void setTilingMode()
    {
        this.tilingMode = true;
    }

    public void setControllerTriggered()
    {
        this.controllerTriggered = true;
    }

    public void getGeometryInfo()
    {
        geometryInfo = new GeometryInfo(spaceShape);
    }


    public void setRoomType()
    {
        if(spaceShape == GeometryInfo.SpaceShape.RoomType)
        {
            GameObject.Find("TransparentWallsForSquare").gameObject.SetActive(false);
            for(int i = 0; i < GameObject.Find("TransparentWallsForRoom").transform.childCount; i++)
            {
                GameObject.Find("TransparentWallsForRoom").transform.GetChild(i).gameObject.SetActive(true);
            }
            roomTypeName = "ROOM";
        }
        else if(spaceShape == GeometryInfo.SpaceShape.SquareType)
        {
            GameObject.Find("TransparentWallsForRoom").gameObject.SetActive(false);
            for(int i = 0; i < GameObject.Find("TransparentWallsForSquare").transform.childCount; i++)
            {
                GameObject.Find("TransparentWallsForSquare").transform.GetChild(i).gameObject.SetActive(true);
            }
            roomTypeName = "SQUARE";
        }
    }

    public void setDoorSetting()
    {
        if(tilingMode)
        {
            if(spaceShape == GeometryInfo.SpaceShape.RoomType)
            {
                for(int i = 0; i < GameObject.Find("DoorsForRoom").transform.childCount; i++)
                {
                    GameObject.Find("DoorsForRoom").transform.GetChild(i).gameObject.SetActive(true);
                }
            }
            else if(spaceShape == GeometryInfo.SpaceShape.SquareType)
            {
                for(int i = 0; i < GameObject.Find("DoorsForSquare").transform.childCount; i++)
                {
                    GameObject.Find("DoorsForSquare").transform.GetChild(i).gameObject.SetActive(true);
                }
            }
        }
    }

    public void ExitGame()
    {
        #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
        #else
                Application.Quit(); // 어플리케이션 종료
        #endif
    }

    //void GetCylinderCollisionTrigger()
    //{
    //    if(roomTypeName == "SQUARE")
    //    {
    //        cylinderCollisionTrigger = GameObject.Find("Terrain/TilingObstaclesForSquare/Cylinders").transform.gameObject.GetComponent<CylinderCollisionTrigger>();
    //    }
    //    else if(roomTypeName == "ROOM")
    //    {
    //        cylinderCollisionTrigger = GameObject.Find("Terrain/TilingObstaclesForRoom/Cylinders").transform.gameObject.GetComponent<CylinderCollisionTrigger>();
    //    }
        
    //    //Debug.Log(cylinderCollisionTrigger.name);
    //    //cylinderCollisionTrigger = GameObject.Find("Terrain").transform.gameObject.GetComponentInChildren<CylinderCollisionTrigger>();

    //    //Debug.Log("abccddd");
    //    // if(roomTypeName=="SQUARE")
    //    // {
    //    //     GameObject.Find("Terrain/TilingObstaclesForSquare/Cylinders").transform.GetChild(randomized[0]).gameObject.SetActive(true);
    //    //     GameObject.Find("Terrain/TilingObstaclesForSquare/Cylinders").transform.GetChild(randomized[1]).gameObject.SetActive(false);
    //    //     GameObject.Find("Terrain/TilingObstaclesForSquare/Cylinders").transform.GetChild(randomized[2]).gameObject.SetActive(false);
    //    //     GameObject.Find("Terrain/TilingObstaclesForSquare/Cylinders").transform.GetChild(randomized[3]).gameObject.SetActive(false);
    //    // }
    //    // else if(roomTypeName=="ROOM")
    //    // {
    //    //     GameObject.Find("Terrain/TilingObstaclesForRoom/Cylinders").transform.GetChild(randomized[0]).gameObject.SetActive(true);
    //    //     GameObject.Find("Terrain/TilingObstaclesForRoom/Cylinders").transform.GetChild(randomized[1]).gameObject.SetActive(false);
    //    //     GameObject.Find("Terrain/TilingObstaclesForRoom/Cylinders").transform.GetChild(randomized[2]).gameObject.SetActive(false);
    //    //     GameObject.Find("Terrain/TilingObstaclesForRoom/Cylinders").transform.GetChild(randomized[3]).gameObject.SetActive(false);
    //    // }
    //}


    //void setCylinderCollisionTrigger()
    //{
    //    if (cylinderCollisionTrigger != null)
    //    {
    //        cylinderCollisionTrigger.redirectionManager = this;
    //        cylinderCollisionTrigger.bodyCollider = body.GetComponentInChildren<CapsuleCollider>();
    //    }
    //    cylinderCollisionTrigger.Initialize();
            
    //}
    public void setNeedChange()
    {
        int index =0;
        if(initialIndex < 3)
        {
            initialIndex++;
            index = randomized[initialIndex];
        }
        else
        {
            initialIndex = 0;
            index = randomized[0];
        }

        //if(index == 0)
        //{
        //    needChange1 = true;
        //    needChange2 = false;
        //    needChange3 = false;
        //    needChange4 = false;
        //}
        //else if(index == 1)
        //{
        //    needChange1 = false;
        //    needChange2 = true;
        //    needChange3 = false;
        //    needChange4 = false;
        //}
        //else if(index == 2)
        //{
        //    needChange1 = false;
        //    needChange2 = false;
        //    needChange3 = true;
        //    needChange4 = false;
        //}
        //else if(index == 3)
        //{
        //    needChange1 = false;
        //    needChange2 = false;
        //    needChange3 = false;
        //    needChange4 = true;
        //}
        

    }
}
