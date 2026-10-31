using System.Threading;
using Cysharp.Threading.Tasks;
using Hisui.UI.Components;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// サブシーン（BuildIndex = 1）用のデモコントローラー。
/// メインシーン（BuildIndex = 0）への戻り遷移を制御する。
/// </summary>
public class HisuiUISubSceneDemo : MonoBehaviour
{
    [Header("LoadingScreen (Optional)")]
    [Tooltip("LoadingScreen コンポーネント（未指定時は DontDestroyOnLoad された LoadingScreen.Instance を自動使用）")]
    [SerializeField] private LoadingScreen _loadingScreen;

    [Header("uGUI Buttons")]
    [Tooltip("メインシーン (BuildIndex = 0) に戻るボタン")]
    [SerializeField] private Button _backToMainSceneButton;

    private LoadingScreen TargetLoadingScreen => _loadingScreen != null ? _loadingScreen : LoadingScreen.Instance;

    private void Awake()
    {
        if (_backToMainSceneButton != null)
        {
            _backToMainSceneButton.onClick.AddListener(BackToMainScene);
        }
    }

    private void OnDestroy()
    {
        if (_backToMainSceneButton != null)
        {
            _backToMainSceneButton.onClick.RemoveListener(BackToMainScene);
        }
    }

    /// <summary>
    /// メインシーン（BuildIndex = 0）にローディング画面を伴って戻る。
    /// </summary>
    public void BackToMainScene()
    {
        BackToMainSceneAsync().Forget();
    }

    /// <summary>
    /// メインシーン（BuildIndex = 0）への遷移を非同期で実行する。
    /// </summary>
    public async UniTask BackToMainSceneAsync(CancellationToken ct = default)
    {
        var loading = TargetLoadingScreen;
        Debug.Log("[HisuiUI SubScene Demo] Back to MainScene (BuildIndex = 0)...");

        if (loading != null)
        {
            await loading.LoadSceneAsync(0, LoadSceneMode.Single, null, ct);
        }
        else
        {
            Debug.LogWarning("[HisuiUI SubScene Demo] LoadingScreen not found. Loading scene directly.");
            await SceneManager.LoadSceneAsync(0, LoadSceneMode.Single).WithCancellation(ct);
        }
    }
}
