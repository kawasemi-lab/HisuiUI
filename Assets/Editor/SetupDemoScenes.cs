using System.Collections.Generic;
using System.IO;
using Hisui.UI.Components;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hisui.UI.Editor
{
    [InitializeOnLoad]
    public static class SetupDemoScenes
    {
        private const string KeyDone = "Hisui_SetupDemoScenes_Done_v1";

        static SetupDemoScenes()
        {
            EditorApplication.delayCall += () =>
            {
                if (!SessionState.GetBool(KeyDone, false))
                {
                    SessionState.SetBool(KeyDone, true);
                    Setup();
                }
            };
        }

        [MenuItem("Hisui/Setup Demo Scenes & UI")]
        public static void Setup()
        {
            SetupSubScene();
            UpdateSampleScene();
            UpdateBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Hisui Setup] Successfully updated SampleScene and created SubScene!");
        }

        private static void SetupSubScene()
        {
            string subScenePath = "Assets/Samples/Scenes/SubScene.unity";
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            // 1. Camera
            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            camGo.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.15f, 0.2f);
            camGo.AddComponent<AudioListener>();

            // 2. EventSystem
            var eventGo = new GameObject("EventSystem");
            eventGo.AddComponent<EventSystem>();
            eventGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

            // 3. Canvas
            var canvasGo = new GameObject("SubSceneCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();

            // 4. Title Text
            var titleGo = new GameObject("TitleText");
            titleGo.transform.SetParent(canvasGo.transform, false);
            var titleRt = titleGo.AddComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.5f, 0.7f);
            titleRt.anchorMax = new Vector2(0.5f, 0.7f);
            titleRt.sizeDelta = new Vector2(800, 100);
            var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
            titleTmp.text = "Sub Scene (BuildIndex = 1)";
            titleTmp.fontSize = 48;
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.color = Color.white;

            // 5. Back Button
            var buttonGo = CreateSimpleButton(canvasGo.transform, "BackToMainButton", "Back to MainScene (0)", new Vector2(0, -50), new Vector2(360, 80));
            var button = buttonGo.GetComponent<Button>();

            // 6. SubSceneController
            var ctrlGo = new GameObject("SubSceneController");
            var demo = ctrlGo.AddComponent<HisuiUISubSceneDemo>();

            // SerializedObject で Button 参照をバインド
            var so = new SerializedObject(demo);
            so.FindProperty("_backToMainSceneButton").objectReferenceValue = button;
            so.ApplyModifiedProperties();

            // シーンを移動して保存
            SceneManager.MoveGameObjectToScene(camGo, scene);
            SceneManager.MoveGameObjectToScene(eventGo, scene);
            SceneManager.MoveGameObjectToScene(canvasGo, scene);
            SceneManager.MoveGameObjectToScene(ctrlGo, scene);

            EditorSceneManager.SaveScene(scene, subScenePath);
            EditorSceneManager.CloseScene(scene, true);
        }

        private static void UpdateSampleScene()
        {
            string sampleScenePath = File.Exists("Assets/Samples/Scenes/Demo.unity")
                ? "Assets/Samples/Scenes/Demo.unity"
                : "Assets/Samples/Scenes/SampleScene.unity";
            var scene = EditorSceneManager.OpenScene(sampleScenePath, OpenSceneMode.Single);

            var demo = Object.FindFirstObjectByType<HisuiUIDemo>();
            if (demo == null)
            {
                Debug.LogWarning("HisuiUIDemo not found in SampleScene.");
                return;
            }

            var sampleCanvas = GameObject.Find("SampleCanvas");
            if (sampleCanvas == null)
            {
                Debug.LogWarning("SampleCanvas not found in SampleScene.");
                return;
            }

            var loadingScreen = Object.FindFirstObjectByType<LoadingScreen>();

            // 1. 全画面パネルの作成（未存在なら作成）
            var fullPanel = sampleCanvas.transform.Find("FullScreenPanel");
            GameObject fullPanelGo;
            Button closeFullPanelBtn;

            if (fullPanel == null)
            {
                fullPanelGo = new GameObject("FullScreenPanel");
                fullPanelGo.transform.SetParent(sampleCanvas.transform, false);
                var rt = fullPanelGo.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                var img = fullPanelGo.AddComponent<Image>();
                img.color = new Color(0.05f, 0.05f, 0.1f, 0.92f);

                // パネルのタイトル
                var pTitle = new GameObject("PanelText");
                pTitle.transform.SetParent(fullPanelGo.transform, false);
                var pRt = pTitle.AddComponent<RectTransform>();
                pRt.anchorMin = new Vector2(0.5f, 0.6f);
                pRt.anchorMax = new Vector2(0.5f, 0.6f);
                pRt.sizeDelta = new Vector2(600, 100);
                var pTmp = pTitle.AddComponent<TextMeshProUGUI>();
                pTmp.text = "Full Screen Panel\n(Loaded via LoadingScreen.RunAsync)";
                pTmp.fontSize = 36;
                pTmp.alignment = TextAlignmentOptions.Center;
                pTmp.color = Color.white;

                // 閉じるボタン
                var closeBtnGo = CreateSimpleButton(fullPanelGo.transform, "CloseFullScreenButton", "Close Panel (RunAsync)", new Vector2(0, -60), new Vector2(340, 70));
                closeFullPanelBtn = closeBtnGo.GetComponent<Button>();

                fullPanelGo.SetActive(false); // 初期状態は非アクティブ
            }
            else
            {
                fullPanelGo = fullPanel.gameObject;
                closeFullPanelBtn = fullPanelGo.GetComponentInChildren<Button>(true);
            }

            // 2. ボタン配置用のコンテナを探す
            var controlButtons = sampleCanvas.transform.Find("ControlButtons");
            Transform buttonParent = controlButtons != null ? controlButtons : sampleCanvas.transform;

            // サブシーンへ行くボタン
            var loadSubBtnGo = buttonParent.Find("LoadSubSceneButton");
            Button loadSubBtn;
            if (loadSubBtnGo == null)
            {
                var go = CreateSimpleButton(buttonParent, "LoadSubSceneButton", "Load SubScene (1)", new Vector2(0, -220), new Vector2(280, 60));
                loadSubBtn = go.GetComponent<Button>();
            }
            else
            {
                loadSubBtn = loadSubBtnGo.GetComponent<Button>();
            }

            // 全画面パネルを開くボタン
            var openPanelBtnGo = buttonParent.Find("ShowFullScreenPanelButton");
            Button openPanelBtn;
            if (openPanelBtnGo == null)
            {
                var go = CreateSimpleButton(buttonParent, "ShowFullScreenPanelButton", "Open FullPanel (RunAsync)", new Vector2(0, -290), new Vector2(280, 60));
                openPanelBtn = go.GetComponent<Button>();
            }
            else
            {
                openPanelBtn = openPanelBtnGo.GetComponent<Button>();
            }

            // 3. HisuiUIDemo の SerializedObject を更新
            var so = new SerializedObject(demo);
            so.Update();

            if (loadingScreen != null)
            {
                so.FindProperty("_loadingScreen").objectReferenceValue = loadingScreen;
            }
            so.FindProperty("_fullScreenPanel").objectReferenceValue = fullPanelGo;
            so.FindProperty("_loadSubSceneButton").objectReferenceValue = loadSubBtn;
            so.FindProperty("_showFullScreenPanelButton").objectReferenceValue = openPanelBtn;
            if (closeFullPanelBtn != null)
            {
                so.FindProperty("_hideFullScreenPanelButton").objectReferenceValue = closeFullPanelBtn;
            }

            so.ApplyModifiedProperties();
            EditorSceneManager.SaveScene(scene);
        }

        private static GameObject CreateSimpleButton(Transform parent, string name, string label, Vector2 anchoredPos, Vector2 size)
        {
            var btnGo = new GameObject(name);
            btnGo.transform.SetParent(parent, false);

            var rt = btnGo.AddComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var img = btnGo.AddComponent<Image>();
            img.color = new Color(0.2f, 0.45f, 0.7f, 1f);

            var btn = btnGo.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.3f, 0.55f, 0.85f, 1f);
            colors.pressedColor = new Color(0.15f, 0.35f, 0.55f, 1f);
            btn.colors = colors;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(btnGo.transform, false);
            var textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 24;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            return btnGo;
        }

        private static void UpdateBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene("Assets/Samples/Scenes/SampleScene.unity", true),
                new EditorBuildSettingsScene("Assets/Samples/Scenes/SubScene.unity", true)
            };
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
