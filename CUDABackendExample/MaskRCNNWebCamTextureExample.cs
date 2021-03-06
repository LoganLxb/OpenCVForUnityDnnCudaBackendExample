#if !(PLATFORM_LUMIN && !UNITY_EDITOR)

#if !UNITY_WSA_10_0

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.DnnModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.UnityUtils;
using OpenCVForUnity.UnityUtils.Helper;
using OpenCVForUnity.UtilsModule;

namespace OpenCVForUnityExample
{
    /// <summary>
    /// MaskRCNNWebCamTextureExample
    /// </summary>
    [RequireComponent (typeof(WebCamTextureToMatHelper))]
    public class MaskRCNNWebCamTextureExample : MonoBehaviour
    {
        /// <summary>
        /// The texture.
        /// </summary>
        Texture2D texture;

        /// <summary>
        /// The webcam texture to mat helper.
        /// </summary>
        WebCamTextureToMatHelper webCamTextureToMatHelper;

        /// <summary>
        /// The bgr mat.
        /// </summary>
        Mat bgrMat;

        /// <summary>
        /// The BLOB.
        /// </summary>
        Mat blob;

        /// <summary>
        /// The net.
        /// </summary>
        Net net;

        /// <summary>
        /// The FPS monitor.
        /// </summary>
        FpsMonitor fpsMonitor;

        const int width = 800;
        const int height = 800;

        const float thr = 0.6f;
        const float mask_thr = 0.5f;

        List<string> classNames;
        List<Scalar> classColors;

        /// <summary>
        /// CLASSES_FILENAME
        /// </summary>
        protected static readonly string CLASSES_FILENAME = "dnn/mscoco_labels.names";

        /// <summary>
        /// The classes filepath.
        /// </summary>
        string classes_filepath;

        /// <summary>
        /// MODEL_FILENAME
        /// </summary>
        protected static readonly string MODEL_FILENAME = "dnn/mask_rcnn_inception_v2_coco_2018_01_28.pb";

        /// <summary>
        /// The model filepath.
        /// </summary>
        string model_filepath;

        /// <summary>
        /// CONFIG_FILENAME
        /// </summary>
        protected static readonly string CONFIG_FILENAME = "dnn/mask_rcnn_inception_v2_coco_2018_01_28.pbtxt";

        /// <summary>
        /// The config filepath.
        /// </summary>
        string config_filepath;

#if UNITY_WEBGL && !UNITY_EDITOR
        IEnumerator getFilePath_Coroutine;
#endif

        // Use this for initialization
        void Start ()
        {
            fpsMonitor = GetComponent<FpsMonitor> ();

            webCamTextureToMatHelper = gameObject.GetComponent<WebCamTextureToMatHelper> ();

#if UNITY_WEBGL && !UNITY_EDITOR
getFilePath_Coroutine = GetFilePath ();
StartCoroutine (getFilePath_Coroutine);
#else

            classes_filepath = Utils.getFilePath(CLASSES_FILENAME);
            model_filepath = Utils.getFilePath(MODEL_FILENAME);
            config_filepath = Utils.getFilePath(CONFIG_FILENAME);
            Run();
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        private IEnumerator GetFilePath ()
        {

            var getFilePathAsync_0_Coroutine = Utils.getFilePathAsync (CLASSES_FILENAME, (result) => {
                classes_filepath = result;
            });
            yield return getFilePathAsync_0_Coroutine;

            var getFilePathAsync_1_Coroutine = Utils.getFilePathAsync (MODEL_FILENAME, (result) => {
                model_filepath = result;
            });
            yield return getFilePathAsync_1_Coroutine;

            var getFilePathAsync_2_Coroutine = Utils.getFilePathAsync (CONFIG_FILENAME, (result) => {
                config_filepath = result;
            });
            yield return getFilePathAsync_2_Coroutine;

            getFilePath_Coroutine = null;

            Run ();
        }
#endif

        // Use this for initialization
        void Run ()
        {
            //if true, The error log of the Native side OpenCV will be displayed on the Unity Editor Console.
            Utils.setDebugMode (true);


            classNames = readClassNames(classes_filepath);
            if (classNames == null)
            {
                Debug.LogError(classes_filepath + " is not loaded. Please see \"StreamingAssets/dnn/setup_dnn_module.pdf\". ");
            }

            classColors = new List<Scalar>();
            for (int i = 0; i < classNames.Count; i++)
            {
                classColors.Add(new Scalar(UnityEngine.Random.Range(0, 255), UnityEngine.Random.Range(0, 255), UnityEngine.Random.Range(0, 255), 255));
            }


            net = null;

            if (string.IsNullOrEmpty(model_filepath) || string.IsNullOrEmpty(config_filepath))
            {
                Debug.LogError(model_filepath + " or " + config_filepath + " is not loaded. Please see \"StreamingAssets/dnn/setup_dnn_module.pdf\". ");
            }
            else
            {
                net = Dnn.readNetFromTensorflow(model_filepath, config_filepath);

                //net.setPreferableBackend(Dnn.DNN_BACKEND_OPENCV);
                //net.setPreferableTarget(Dnn.DNN_TARGET_CPU);
                net.setPreferableBackend(Dnn.DNN_BACKEND_CUDA);
                net.setPreferableTarget(Dnn.DNN_TARGET_CUDA);

            }

#if UNITY_ANDROID && !UNITY_EDITOR
            // Avoids the front camera low light issue that occurs in only some Android devices (e.g. Google Pixel, Pixel2).
            webCamTextureToMatHelper.avoidAndroidFrontCameraLowLightIssue = true;
#endif
            webCamTextureToMatHelper.Initialize ();
        }

        /// <summary>
        /// Raises the webcam texture to mat helper initialized event.
        /// </summary>
        public void OnWebCamTextureToMatHelperInitialized ()
        {
            Debug.Log ("OnWebCamTextureToMatHelperInitialized");

            Mat webCamTextureMat = webCamTextureToMatHelper.GetMat ();

            texture = new Texture2D (webCamTextureMat.cols (), webCamTextureMat.rows (), TextureFormat.RGBA32, false);
            Utils.fastMatToTexture2D(webCamTextureMat, texture);

            gameObject.GetComponent<Renderer> ().material.mainTexture = texture;

            gameObject.transform.localScale = new Vector3 (webCamTextureMat.cols (), webCamTextureMat.rows (), 1);
            Debug.Log ("Screen.width " + Screen.width + " Screen.height " + Screen.height + " Screen.orientation " + Screen.orientation);

            if (fpsMonitor != null) {
                fpsMonitor.Add ("width", webCamTextureMat.width ().ToString ());
                fpsMonitor.Add ("height", webCamTextureMat.height ().ToString ());
                fpsMonitor.Add ("orientation", Screen.orientation.ToString ());
            }

                                    
            float width = webCamTextureMat.width ();
            float height = webCamTextureMat.height ();
                                    
            float widthScale = (float)Screen.width / width;
            float heightScale = (float)Screen.height / height;
            if (widthScale < heightScale) {
                Camera.main.orthographicSize = (width * (float)Screen.height / (float)Screen.width) / 2;
            } else {
                Camera.main.orthographicSize = height / 2;
            }

            bgrMat = new Mat (webCamTextureMat.rows (), webCamTextureMat.cols (), CvType.CV_8UC3);
        }

        /// <summary>
        /// Raises the webcam texture to mat helper disposed event.
        /// </summary>
        public void OnWebCamTextureToMatHelperDisposed ()
        {
            Debug.Log ("OnWebCamTextureToMatHelperDisposed");

            if (bgrMat != null)
                bgrMat.Dispose ();

            if (texture != null) {
                Texture2D.Destroy (texture);
                texture = null;
            }
        }

        /// <summary>
        /// Raises the webcam texture to mat helper error occurred event.
        /// </summary>
        /// <param name="errorCode">Error code.</param>
        public void OnWebCamTextureToMatHelperErrorOccurred (WebCamTextureToMatHelper.ErrorCode errorCode)
        {
            Debug.Log ("OnWebCamTextureToMatHelperErrorOccurred " + errorCode);
        }

        // Update is called once per frame
        void Update ()
        {
            if (webCamTextureToMatHelper.IsPlaying () && webCamTextureToMatHelper.DidUpdateThisFrame ()) {

                Mat rgbaMat = webCamTextureToMatHelper.GetMat ();

                if (net == null)
                {

                    Imgproc.putText(rgbaMat, "model file is not loaded.", new Point(5, rgbaMat.rows() - 30), Imgproc.FONT_HERSHEY_SIMPLEX, 0.7, new Scalar(255, 255, 255, 255), 2, Imgproc.LINE_AA, false);
                    Imgproc.putText(rgbaMat, "Please read console message.", new Point(5, rgbaMat.rows() - 10), Imgproc.FONT_HERSHEY_SIMPLEX, 0.7, new Scalar(255, 255, 255, 255), 2, Imgproc.LINE_AA, false);

                }
                else
                {
                    Imgproc.cvtColor(rgbaMat, bgrMat, Imgproc.COLOR_RGBA2BGR);

                    float frameWidth = bgrMat.cols();
                    float frameHeight = bgrMat.rows();

                    blob = Dnn.blobFromImage(bgrMat, 1.0, new Size(width, height), new Scalar(0, 0, 0), true, false);


                    net.setInput(blob);



                    List<Mat> outputBlobs = new List<Mat>();
                    List<string> outputName = new List<string>();
                    outputName.Add("detection_out_final");
                    outputName.Add("detection_masks");

                    net.forward(outputBlobs, outputName);

                    Mat boxes = outputBlobs[0];
                    Mat masks = outputBlobs[1];



                    //reshape from 4D to two 2D.
                    float[] data = new float[boxes.size(3)];
                    boxes = boxes.reshape(1, (int)boxes.total() / boxes.size(3));
                    //              Debug.Log ("boxes.ToString() " + boxes.ToString ());

                    //reshape from 4D to two 2D.
                    float[] mask_data = new float[masks.size(2) * masks.size(3)];
                    masks = masks.reshape(1, (int)masks.total() / (masks.size(2) * masks.size(3)));
                    //              Debug.Log ("masks.ToString(): " + masks.ToString ());


                    for (int i = 0; i < boxes.rows(); i++)
                    {

                        boxes.get(i, 0, data);

                        float score = data[2];

                        if (score > thr)
                        {
                            int class_id = (int)(data[1]);


                            float left = (float)(data[3] * frameWidth);
                            float top = (float)(data[4] * frameHeight);
                            float right = (float)(data[5] * frameWidth);
                            float bottom = (float)(data[6] * frameHeight);

                            left = (int)Mathf.Max(0, Mathf.Min(left, frameWidth - 1));
                            top = (int)Mathf.Max(0, Mathf.Min(top, frameHeight - 1));
                            right = (int)Mathf.Max(0, Mathf.Min(right, frameWidth - 1));
                            bottom = (int)Mathf.Max(0, Mathf.Min(bottom, frameHeight - 1));

                            //Debug.Log("class_id: " + class_id + " class_name " + classNames[class_id] + " left: " + left + " top: " + top + " right: " + right + " bottom: " + bottom);




                            //draw masks

                            masks.get((i * 90) + class_id, 0, mask_data);

                            Mat objectMask = new Mat(15, 15, CvType.CV_32F);
                            MatUtils.copyToMat<float>(mask_data, objectMask);

                            Imgproc.resize(objectMask, objectMask, new Size(right - left + 1, bottom - top + 1));

                            Core.compare(objectMask, new Scalar(mask_thr), objectMask, Core.CMP_GT);
                            //                        Debug.Log ("objectMask.ToString(): " + objectMask.ToString ());
                            //                        Debug.Log ("objectMask.dump(): " + objectMask.dump ());


                            Mat roi = new Mat(rgbaMat, new OpenCVForUnity.CoreModule.Rect(new Point(left, top), new Point(right + 1, bottom + 1)));

                            Mat coloredRoi = new Mat(roi.size(), CvType.CV_8UC4);

                            Imgproc.rectangle(coloredRoi, new Point(0, 0), new Point(coloredRoi.width(), coloredRoi.height()), classColors[class_id], -1);

                            Core.addWeighted(coloredRoi, 0.7, roi, 0.3, 0, coloredRoi);
                            //                        Debug.Log ("coloredRoi.ToString(): " + coloredRoi.ToString ());
                            //                        Debug.Log ("roi.ToString(): " + roi.ToString ());

                            coloredRoi.copyTo(roi, objectMask);
                            coloredRoi.Dispose();

                            objectMask.Dispose();




                            //draw boxes

                            Imgproc.rectangle(rgbaMat, new Point(left, top), new Point(right, bottom), new Scalar(0, 255, 0, 255), 2);

                            string label = score.ToString();
                            if (classNames != null && classNames.Count != 0)
                            {
                                if (class_id < (int)classNames.Count)
                                {
                                    label = classNames[class_id] + ": " + label;
                                }
                            }

                            int[] baseLine = new int[1];
                            Size labelSize = Imgproc.getTextSize(label, Imgproc.FONT_HERSHEY_SIMPLEX, 0.5, 1, baseLine);

                            top = Mathf.Max(top, (int)labelSize.height);
                            Imgproc.rectangle(rgbaMat, new Point(left, top - labelSize.height),
                                new Point(left + labelSize.width, top + baseLine[0]), Scalar.all(255), Core.FILLED);
                            Imgproc.putText(rgbaMat, label, new Point(left, top), Imgproc.FONT_HERSHEY_SIMPLEX, 0.5, new Scalar(0, 0, 0, 255));


                        }
                    }

                    boxes.Dispose();
                    masks.Dispose();
                    blob.Dispose();

                }

                Utils.fastMatToTexture2D (rgbaMat, texture);
            }
        }

        /// <summary>
        /// Raises the disable event.
        /// </summary>
        void OnDisable ()
        {
            webCamTextureToMatHelper.Dispose ();

            if (blob != null)
                blob.Dispose ();
            if (net != null)
                net.Dispose ();

            if (bgrMat!= null)
                bgrMat.Dispose();

            Utils.setDebugMode (false);

            #if UNITY_WEBGL && !UNITY_EDITOR
            if (getFilePath_Coroutine != null) {
                StopCoroutine (getFilePath_Coroutine);
                ((IDisposable)getFilePath_Coroutine).Dispose ();
            }
            #endif
        }

        /// <summary>
        /// Raises the back button click event.
        /// </summary>
        public void OnBackButtonClick ()
        {
            SceneManager.LoadScene ("OpenCVForUnityExample");
        }

        /// <summary>
        /// Raises the play button click event.
        /// </summary>
        public void OnPlayButtonClick ()
        {
            webCamTextureToMatHelper.Play ();
        }

        /// <summary>
        /// Raises the pause button click event.
        /// </summary>
        public void OnPauseButtonClick ()
        {
            webCamTextureToMatHelper.Pause ();
        }

        /// <summary>
        /// Raises the stop button click event.
        /// </summary>
        public void OnStopButtonClick ()
        {
            webCamTextureToMatHelper.Stop ();
        }

        /// <summary>
        /// Raises the change camera button click event.
        /// </summary>
        public void OnChangeCameraButtonClick ()
        {
            webCamTextureToMatHelper.requestedIsFrontFacing = !webCamTextureToMatHelper.IsFrontFacing ();
        }

        /// <summary>
        /// Reads the class names.
        /// </summary>
        /// <returns>The class names.</returns>
        /// <param name="filename">Filename.</param>
        private List<string> readClassNames(string filename)
        {
            List<string> classNames = new List<string>();

            System.IO.StreamReader cReader = null;
            try
            {
                cReader = new System.IO.StreamReader(filename, System.Text.Encoding.Default);

                while (cReader.Peek() >= 0)
                {
                    string name = cReader.ReadLine();
                    classNames.Add(name);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError(ex.Message);
                return null;
            }
            finally
            {
                if (cReader != null)
                    cReader.Close();
            }

            return classNames;
        }
    }
}
#endif

#endif