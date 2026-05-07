using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

public class AuthHandler : MonoBehaviour
{
    private string Token;
    private string Username;

    [SerializeField] private GameObject panelLogin;
    [SerializeField] private GameObject panelDashboard;
    [SerializeField] private GameObject panelRegister;

    private string apiUrl = "http://127.0.0.1:1235";

    [SerializeField] private TMP_InputField usernameInputField;
    [SerializeField] private TMP_InputField passwordInputField;
    [SerializeField] private TMP_InputField registerUsernameInputField;
    [SerializeField] private TMP_InputField registerPasswordInputField;
    [SerializeField] private TMP_Text usernameLabel;

    [SerializeField] private Transform leaderboardContainer;
    [SerializeField] private GameObject scoreItemPrefab;

    [SerializeField] private TMP_Text caloriasText;
    private int calorias = 0;

    private void Start()
    {
        usernameInputField = GameObject.Find("InputFieldUsername").GetComponent<TMP_InputField>();
        passwordInputField = GameObject.Find("InputFieldPassword").GetComponent<TMP_InputField>();
        registerUsernameInputField = GameObject.Find("InputFieldRegisterUsername").GetComponent<TMP_InputField>();
        registerPasswordInputField = GameObject.Find("InputFieldRegisterPassword").GetComponent<TMP_InputField>();
        usernameLabel = GameObject.Find("LabelUsername").GetComponent<TMP_Text>();

        panelLogin = GameObject.Find("PanelLogin");
        panelDashboard = GameObject.Find("PanelDashboard");
        panelRegister = GameObject.Find("PanelRegister");

        caloriasText = GameObject.Find("Text_Calorias").GetComponent<TMP_Text>();
        caloriasText.text = "Calorías: 0";

        panelDashboard.SetActive(false);
        panelRegister.SetActive(false);
        panelLogin.SetActive(true);

        Token = PlayerPrefs.GetString("Token", null);
        Username = PlayerPrefs.GetString("Username", null);

        if (!string.IsNullOrEmpty(Token) && !string.IsNullOrEmpty(Username))
        {
            StartCoroutine(GetProfile());
        }
        else
        {
            Debug.Log("No hay token, el usuario debe loguearse.");
        }
    }

    // ─── GET PROFILE ───────────────────────────────────────────
    public IEnumerator GetProfile()
    {
        UnityWebRequest www = UnityWebRequest.Get(apiUrl + "/api/usuarios/" + Username);
        www.SetRequestHeader("x-token", Token);
        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("Token válido, entrando al dashboard.");
            AuthResponse response = JsonUtility.FromJson<AuthResponse>(www.downloadHandler.text);
            Username = response.usuario.username;
            SetUIForUserLogged();
        }
        else
        {
            Debug.LogError("Token inválido o expirado, regresando al login.");
            Logout();
        }
    }

    // ─── LOGIN ─────────────────────────────────────────────────
    public void LoginButtonHandler()
    {
        string username = usernameInputField.text;
        string password = passwordInputField.text;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            Debug.LogError("Usuario y contraseña son requeridos.");
            return;
        }

        StartCoroutine(LoginCoroutine(username, password));
    }

    IEnumerator LoginCoroutine(string username, string password)
    {
        AuthData authData = new AuthData { username = username, password = password };
        string jsonData = JsonUtility.ToJson(authData);

        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
        UnityWebRequest www = new UnityWebRequest(apiUrl + "/api/auth/login", "POST");
        www.uploadHandler = new UploadHandlerRaw(bodyRaw);
        www.downloadHandler = new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");

        yield return www.SendWebRequest();

        Debug.Log("Respuesta login: " + www.downloadHandler.text);

        if (www.result == UnityWebRequest.Result.Success)
        {
            AuthResponse authResponse = JsonUtility.FromJson<AuthResponse>(www.downloadHandler.text);

            if (authResponse != null && !string.IsNullOrEmpty(authResponse.token))
            {
                Token = authResponse.token;
                Username = authResponse.usuario.username;

                PlayerPrefs.SetString("Token", Token);
                PlayerPrefs.SetString("Username", Username);

                Debug.Log("Login exitoso: " + Username);
                SetUIForUserLogged();
            }
            else
            {
                Debug.LogError("Respuesta inválida del servidor.");
            }
        }
        else
        {
            Debug.LogError("Login fallido: " + www.error + " | " + www.downloadHandler.text);
        }
    }

    // ─── REGISTRO ──────────────────────────────────────────────
    public void RegisterButtonHandler()
    {
        string username = registerUsernameInputField.text;
        string password = registerPasswordInputField.text;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            Debug.LogError("Usuario y contraseña son requeridos.");
            return;
        }

        StartCoroutine(RegisterCoroutine(username, password));
    }

    IEnumerator RegisterCoroutine(string username, string password)
    {
        AuthData data = new AuthData { username = username, password = password };
        string jsonData = JsonUtility.ToJson(data);

        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
        UnityWebRequest www = new UnityWebRequest(apiUrl + "/api/usuarios", "POST");
        www.uploadHandler = new UploadHandlerRaw(bodyRaw);
        www.downloadHandler = new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");

        yield return www.SendWebRequest();

        Debug.Log("Respuesta registro: " + www.downloadHandler.text);

        if (www.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("Registro exitoso, ve al login.");
            ShowLogin();
        }
        else
        {
            Debug.LogError("Registro fallido: " + www.error + " | " + www.downloadHandler.text);
        }
    }

    // ─── SEND SCORE ────────────────────────────────────────────
    public void SendScore()
    {
        StartCoroutine(UpdateScoreCoroutine(calorias));
    }

    IEnumerator UpdateScoreCoroutine(int newScore)
    {
        // 1. Obtener score actual
        UnityWebRequest getRequest = UnityWebRequest.Get(apiUrl + "/api/usuarios/" + Username);
        getRequest.SetRequestHeader("x-token", Token);
        yield return getRequest.SendWebRequest();

        if (getRequest.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Error al obtener score actual: " + getRequest.error);
            yield break;
        }

        AuthResponse currentResponse = JsonUtility.FromJson<AuthResponse>(getRequest.downloadHandler.text);
        int currentScore = currentResponse.usuario.data.score;

        Debug.Log("Score actual: " + currentScore + " | Nuevo score: " + newScore);

        // 2. Solo actualizar si es mayor
        if (newScore <= currentScore)
        {
            Debug.Log("El nuevo score no es mayor, no se actualiza.");
            yield break;
        }

        // 3. Mandar PATCH
        ScoreUpdate update = new ScoreUpdate
        {
            username = Username,
            data = new UserData { score = newScore }
        };

        string jsonData = JsonUtility.ToJson(update);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);

        UnityWebRequest patchRequest = new UnityWebRequest(apiUrl + "/api/usuarios", "PATCH");
        patchRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
        patchRequest.downloadHandler = new DownloadHandlerBuffer();
        patchRequest.SetRequestHeader("Content-Type", "application/json");
        patchRequest.SetRequestHeader("x-token", Token);

        yield return patchRequest.SendWebRequest();

        if (patchRequest.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("Score actualizado correctamente.");
        }
        else
        {
            Debug.LogError("Error al actualizar score: " + patchRequest.error + " | " + patchRequest.downloadHandler.text);
        }
    }

    // ─── GET SCORES ────────────────────────────────────────────
    public void GetScores()
    {
        StartCoroutine(GetScoresCoroutine());
    }

    IEnumerator GetScoresCoroutine()
    {
        Debug.Log("Obteniendo scores...");
        UnityWebRequest www = UnityWebRequest.Get(apiUrl + "/api/usuarios");
        www.SetRequestHeader("x-token", Token);
        yield return www.SendWebRequest();

        Debug.Log("Respuesta scores: " + www.downloadHandler.text);

        if (www.result == UnityWebRequest.Result.Success)
        {
            UsersResponse response = JsonUtility.FromJson<UsersResponse>(www.downloadHandler.text);

            if (response == null || response.usuarios == null)
            {
                Debug.LogError("No se pudieron parsear los usuarios.");
                yield break;
            }

            response.usuarios.Sort((a, b) => b.data.score.CompareTo(a.data.score));
            MostrarLeaderboard(response.usuarios);
        }
        else
        {
            Debug.LogError("Error al obtener scores: " + www.error + " | " + www.downloadHandler.text);
        }
    }

    void MostrarLeaderboard(List<User> users)
    {
        if (leaderboardContainer == null) { Debug.LogError("leaderboardContainer es null!"); return; }
        if (scoreItemPrefab == null) { Debug.LogError("scoreItemPrefab es null!"); return; }

        foreach (Transform child in leaderboardContainer)
            Destroy(child.gameObject);

        int count = Mathf.Min(10, users.Count);
        for (int i = 0; i < count; i++)
        {
            GameObject item = Instantiate(scoreItemPrefab, leaderboardContainer);
            TMP_Text[] texts = item.GetComponentsInChildren<TMP_Text>();
            texts[0].text = users[i].username;
            texts[1].text = users[i].data.score.ToString();
        }
    }

    // ─── LOGOUT ────────────────────────────────────────────────
    public void Logout()
    {
        Token = null;
        Username = null;
        PlayerPrefs.DeleteKey("Token");
        PlayerPrefs.DeleteKey("Username");

        panelDashboard.SetActive(false);
        panelRegister.SetActive(false);
        panelLogin.SetActive(true);

        Debug.Log("Sesión cerrada.");
    }

    // ─── UI ────────────────────────────────────────────────────
    public void SetUIForUserLogged()
    {
        panelLogin.SetActive(false);
        panelRegister.SetActive(false);
        panelDashboard.SetActive(true);

        calorias = 0;
        caloriasText.text = "Calorías: 0";
        usernameLabel.text = "Welcome, " + Username;

        GetScores();
    }

    public void ShowRegister()
    {
        panelLogin.SetActive(false);
        panelRegister.SetActive(true);
        panelDashboard.SetActive(false);
    }

    public void ShowLogin()
    {
        panelRegister.SetActive(false);
        panelDashboard.SetActive(false);
        panelLogin.SetActive(true);
    }

    public void AddCaloria()
    {
        calorias++;
        caloriasText.text = "Calorías: " + calorias;
    }
}

// ─── MODELOS ───────────────────────────────────────────────────
[System.Serializable]
public class AuthData
{
    public string username;
    public string password;
}

[System.Serializable]
public class UsersResponse
{
    public List<User> usuarios;
}

[System.Serializable]
public class ScoreUpdate
{
    public string username;
    public UserData data;
}

[System.Serializable]
public class AuthResponse
{
    public User usuario;
    public string token;
}

[System.Serializable]
public class User
{
    public string _id;
    public string username;
    public UserData data;
}

[System.Serializable]
public class UserData
{
    public int score;
}