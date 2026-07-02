using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject onlinePanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Online Panel")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private TMP_Text joinCodeDisplay;
    [SerializeField] private Button toggleCodeVisibilityButton;
    [SerializeField] private Button copyCodeButton;
    [SerializeField] private Button cancelHostButton;
    [SerializeField] private TMP_Text statusText;

    private const string GameScene = "NetcodeTest";
    private const string HiddenCodeText = "Join Code: ••••••";

    private string _currentJoinCode;
    private bool _joinCodeVisible = true;

    private void Start()
    {
        ShowMain();
    }

    // ─── Main buttons ───────────────────────────────────────────────────────────

    public void OnOfflineClicked()
    {
        SceneManager.LoadScene(GameScene);
    }

    public void OnOnlineClicked()
    {
        ShowOnline();
    }

    public void OnSettingsClicked()
    {
        ShowSettings();
    }

    public void OnExitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ─── Online panel ───────────────────────────────────────────────────────────

    public async void OnHostClicked()
    {
        SetOnlineInteractable(false);
        SetStatus("Creating lobby...");

        // Hide join-related elements immediately — this player is hosting, not joining.
        joinButton.gameObject.SetActive(false);
        joinCodeInput.gameObject.SetActive(false);
        hostButton.gameObject.SetActive(false);

        LobbyManager.Instance.OnJoinCodeReady += code =>
        {
            _currentJoinCode = code;
            _joinCodeVisible = false;
            joinCodeDisplay.text = HiddenCodeText;
            joinCodeDisplay.gameObject.SetActive(true);
            toggleCodeVisibilityButton.gameObject.SetActive(true);
            copyCodeButton.gameObject.SetActive(true);
            cancelHostButton.gameObject.SetActive(true);
            SetStatus("Waiting for player to join...");

            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        };

        LobbyManager.Instance.OnError += msg =>
        {
            SetStatus($"Error: {msg}");
            ShowOnline();
        };

        await LobbyManager.Instance.HostAsync();
    }

    public async void OnCancelHostClicked()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;

        await LobbyManager.Instance.StopHosting();
        ShowOnline();
    }

    public async void OnJoinClicked()
    {
        string code = joinCodeInput.text.Trim();
        if (string.IsNullOrEmpty(code))
        {
            SetStatus("Enter a join code.");
            return;
        }

        SetOnlineInteractable(false);
        SetStatus("Joining...");

        LobbyManager.Instance.OnError += msg =>
        {
            SetStatus($"Error: {msg}");
            SetOnlineInteractable(true);
        };

        // Scene load is server-driven via NGO scene management —
        // the client transitions automatically once connected.
        await LobbyManager.Instance.JoinAsync(code);
    }

    public void OnOnlineBackClicked()
    {
        ShowMain();
    }

    public void OnToggleCodeVisibilityClicked()
    {
        _joinCodeVisible = !_joinCodeVisible;
        joinCodeDisplay.text = _joinCodeVisible ? $"Join Code: {_currentJoinCode}" : HiddenCodeText;
    }

    public void OnCopyCodeClicked()
    {
        if (string.IsNullOrEmpty(_currentJoinCode)) return;
        GUIUtility.systemCopyBuffer = _currentJoinCode;
    }

    private void OnClientConnected(ulong clientId)
    {
        // Host (server) is always connected as clientId 0 — wait for a second, real client.
        if (NetworkManager.Singleton.ConnectedClientsIds.Count < 2) return;

        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        SetStatus("Player joined. Launching...");
        NetworkManager.Singleton.SceneManager.LoadScene(GameScene, LoadSceneMode.Single);
    }

    // ─── Settings panel ─────────────────────────────────────────────────────────

    public void OnSettingsBackClicked()
    {
        ShowMain();
    }

    // ─── Helpers ────────────────────────────────────────────────────────────────

    private void ShowMain()
    {
        mainPanel.SetActive(true);
        onlinePanel.SetActive(false);
        settingsPanel.SetActive(false);
    }

    private void ShowOnline()
    {
        mainPanel.SetActive(false);
        onlinePanel.SetActive(true);
        settingsPanel.SetActive(false);

        hostButton.gameObject.SetActive(true);
        joinButton.gameObject.SetActive(true);
        joinCodeInput.gameObject.SetActive(true);
        joinCodeDisplay.gameObject.SetActive(false);
        toggleCodeVisibilityButton.gameObject.SetActive(false);
        copyCodeButton.gameObject.SetActive(false);
        cancelHostButton.gameObject.SetActive(false);

        SetStatus("");
        SetOnlineInteractable(true);
    }

    private void ShowSettings()
    {
        mainPanel.SetActive(false);
        onlinePanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    private void SetStatus(string msg) => statusText.text = msg;

    private void SetOnlineInteractable(bool interactable)
    {
        hostButton.interactable = interactable;
        joinButton.interactable = interactable;
        joinCodeInput.interactable = interactable;
    }

    private void LoadGame() => SceneManager.LoadScene(GameScene);
}
