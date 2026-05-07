using Firebase;
using Firebase.Auth;
using Firebase.Database;
using Firebase.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// AuthHandler refactorizado para usar Firebase SDK en lugar de API REST
/// Mantiene la estructura similar a tu código original pero con Realtime Database
/// </summary>
public class AuthHandler : MonoBehaviour
{
    // ─── FIREBASE REFERENCES ───────────────────────────────────────────
    private FirebaseAuth firebaseAuth;
    private FirebaseDatabase firebaseDatabase;

    // ─── USUARIO ACTUAL ────────────────────────────────────────────────
    private string CurrentUserId;
    private string Username;
    private int CurrentScore = 0;

    // ─── PANELS ────────────────────────────────────────────────────────
    [SerializeField] private GameObject panelLogin;
    [SerializeField] private GameObject panelDashboard;
    [SerializeField] private GameObject panelRegister;
    [SerializeField] private GameObject panelRecovery;

    // ─── UI INPUTS ─────────────────────────────────────────────────────
    [SerializeField] private TMP_InputField usernameInputField;
    [SerializeField] private TMP_InputField passwordInputField;
    [SerializeField] private TMP_InputField registerUsernameInputField;
    [SerializeField] private TMP_InputField registerPasswordInputField;
    [SerializeField] private TMP_InputField recoveryEmailInputField;

    // ─── UI DISPLAYS ───────────────────────────────────────────────────
    [SerializeField] private TMP_Text usernameLabel;
    [SerializeField] private TMP_Text caloriasText;
    [SerializeField] private TMP_Text statusMessageText;

    // ─── LEADERBOARD ───────────────────────────────────────────────────
    [SerializeField] private Transform leaderboardContainer;
    [SerializeField] private GameObject scoreItemPrefab;

    // ─── STATE ─────────────────────────────────────────────────────────
    private bool isProcessing = false;
    private DatabaseReference userScoreReference;

    private void Start()
    {
        InitializeFirebase();
        FindUIElements();
        CheckExistingSession();
        SetupAuthStateListener();
    }

    // ─── INITIALIZE FIREBASE ────────────────────────────────────────────
    private void InitializeFirebase()
    {
        firebaseAuth = FirebaseAuth.DefaultInstance;
        firebaseDatabase = FirebaseDatabase.DefaultInstance;
        
        Debug.Log("[AuthHandler] Firebase inicializado");
    }

    // ─── FIND UI ELEMENTS ───────────────────────────────────────────────
    private void FindUIElements()
    {
        usernameInputField = GameObject.Find("InputFieldUsername")?.GetComponent<TMP_InputField>();
        passwordInputField = GameObject.Find("InputFieldPassword")?.GetComponent<TMP_InputField>();
        registerUsernameInputField = GameObject.Find("InputFieldRegisterUsername")?.GetComponent<TMP_InputField>();
        registerPasswordInputField = GameObject.Find("InputFieldRegisterPassword")?.GetComponent<TMP_InputField>();
        recoveryEmailInputField = GameObject.Find("InputFieldRecoveryEmail")?.GetComponent<TMP_InputField>();
        usernameLabel = GameObject.Find("LabelUsername")?.GetComponent<TMP_Text>();
        caloriasText = GameObject.Find("Text_Calorias")?.GetComponent<TMP_Text>();
        statusMessageText = GameObject.Find("Text_Status")?.GetComponent<TMP_Text>();

        panelLogin = GameObject.Find("PanelLogin");
        panelDashboard = GameObject.Find("PanelDashboard");
        panelRegister = GameObject.Find("PanelRegister");
        panelRecovery = GameObject.Find("PanelRecovery");

        // Inicializar paneles
        if (panelDashboard) panelDashboard.SetActive(false);
        if (panelRegister) panelRegister.SetActive(false);
        if (panelRecovery) panelRecovery.SetActive(false);
        if (panelLogin) panelLogin.SetActive(true);

        // Inicializar texto de calorías
        if (caloriasText) caloriasText.text = "Calorías: 0";
    }

    // ─── AUTH STATE LISTENER ────────────────────────────────────────────
    /// <summary>
    /// Escucha cambios en el estado de autenticación de Firebase
    /// Similar al código de tu profe pero integrado aquí
    /// </summary>
    private void SetupAuthStateListener()
    {
        firebaseAuth.StateChanged += AuthStateChanged;
    }

    private void AuthStateChanged(object sender, EventArgs e)
    {
        if (firebaseAuth.CurrentUser != null)
        {
            // Usuario está autenticado
            CurrentUserId = firebaseAuth.CurrentUser.UserId;
            Debug.Log("[AuthHandler] Usuario autenticado: " + CurrentUserId);
            StartCoroutine(LoadUserDataCoroutine());
        }
        else
        {
            // Usuario NO está autenticado
            CurrentUserId = null;
            Username = null;
            ShowLoginPanel();
            Debug.Log("[AuthHandler] Usuario no autenticado");
        }
    }

    // ─── CHECK EXISTING SESSION ────────────────────────────────────────
    private void CheckExistingSession()
    {
        // Firebase maneja automáticamente la sesión
        // Si hay un usuario, AuthStateChanged se dispara automáticamente
        Debug.Log("[AuthHandler] Comprobando sesión existente...");
    }

    // ─── LOGIN ─────────────────────────────────────────────────────────
    public void LoginButtonHandler()
    {
        string email = usernameInputField.text.Trim();
        string password = passwordInputField.text;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            ShowMessage("Email y contraseña son requeridos", Color.red);
            return;
        }

        StartCoroutine(LoginCoroutine(email, password));
    }

    private IEnumerator LoginCoroutine(string email, string password)
    {
        isProcessing = true;
        ShowMessage("Iniciando sesión...", Color.blue);

        var loginTask = firebaseAuth.SignInWithEmailAndPasswordAsync(email, password);
        yield return new WaitUntil(() => loginTask.IsCompleted);

        if (loginTask.Exception != null)
        {
            HandleAuthException(loginTask.Exception, "login");
            isProcessing = false;
            yield break;
        }

        FirebaseUser user = loginTask.Result.User;
        CurrentUserId = user.UserId;

        // Cargar datos del usuario
        yield return StartCoroutine(LoadUserDataCoroutine());

        isProcessing = false;
        ShowMessage("¡Login exitoso!", Color.green);
    }

    // ─── REGISTER ──────────────────────────────────────────────────────
    public void RegisterButtonHandler()
    {
        string email = registerUsernameInputField.text.Trim();
        string password = registerPasswordInputField.text;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            ShowMessage("Email y contraseña son requeridos", Color.red);
            return;
        }

        StartCoroutine(RegisterCoroutine(email, password));
    }

    private IEnumerator RegisterCoroutine(string email, string password)
    {
        isProcessing = true;
        ShowMessage("Creando cuenta...", Color.blue);

        var createTask = firebaseAuth.CreateUserWithEmailAndPasswordAsync(email, password);
        yield return new WaitUntil(() => createTask.IsCompleted);

        if (createTask.Exception != null)
        {
            HandleAuthException(createTask.Exception, "registro");
            isProcessing = false;
            yield break;
        }

        FirebaseUser user = createTask.Result.User;
        CurrentUserId = user.UserId;
        Username = email.Split('@')[0]; // Usar parte del email como username

        // Guardar datos en Realtime Database
        yield return StartCoroutine(SaveUserDataCoroutine(user.UserId, email, Username));

        isProcessing = false;
        ShowMessage("¡Registro exitoso!", Color.green);
        yield return new WaitForSeconds(1f);
        ShowLoginPanel();
    }

    // ─── SAVE USER DATA ────────────────────────────────────────────────
    private IEnumerator SaveUserDataCoroutine(string userId, string email, string username)
    {
        var userData = new Dictionary<string, object>
        {
            { "username", username },
            { "email", email },
            { "score", 0 },
            { "createdAt", System.DateTime.Now.ToString() },
            { "lastLogin", System.DateTime.Now.ToString() }
        };

        var task = firebaseDatabase.GetReference("users/" + userId).SetValueAsync(userData);
        yield return new WaitUntil(() => task.IsCompleted);

        if (task.Exception != null)
        {
            Debug.LogError("[AuthHandler] Error guardando datos: " + task.Exception);
            yield break;
        }

        Debug.Log("[AuthHandler] Datos del usuario guardados");
    }

    // ─── LOAD USER DATA ────────────────────────────────────────────────
    private IEnumerator LoadUserDataCoroutine()
    {
        var task = firebaseDatabase.GetReference("users/" + CurrentUserId).GetValueAsync();
        yield return new WaitUntil(() => task.IsCompleted);

        if (task.Exception != null)
        {
            Debug.LogError("[AuthHandler] Error cargando datos: " + task.Exception);
            yield break;
        }

        DataSnapshot snapshot = task.Result;
        if (snapshot.Exists)
        {
            try
            {
                Username = snapshot.Child("username").Value.ToString();
                object scoreObj = snapshot.Child("score").Value;
                CurrentScore = scoreObj != null ? int.Parse(scoreObj.ToString()) : 0;

                // Actualizar UI
                SetUIForUserLogged();

                // Configurar listener en tiempo real para el score
                SetupScoreListener();
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[AuthHandler] Error parseando datos: " + ex.Message);
            }
        }
    }

    // ─── SETUP SCORE LISTENER (SINCRONIZACIÓN EN TIEMPO REAL) ──────────
    /// <summary>
    /// Configura un listener que detecta cambios en el score en tiempo real
    /// Si se cambia el score desde otro dispositivo/sesión, se actualiza aquí
    /// </summary>
    private void SetupScoreListener()
    {
        if (CurrentUserId == null) return;

        userScoreReference = firebaseDatabase.GetReference("users/" + CurrentUserId + "/score");
        userScoreReference.ValueChanged += HandleScoreChanged;

        Debug.Log("[AuthHandler] Score listener configurado");
    }

    private void HandleScoreChanged(object sender, ValueChangedEventArgs args)
    {
        if (args.DatabaseError != null)
        {
            Debug.LogError("[AuthHandler] Error en listener: " + args.DatabaseError.Message);
            return;
        }

        if (args.Snapshot.Exists)
        {
            try
            {
                CurrentScore = int.Parse(args.Snapshot.Value.ToString());
                UpdateCaloriasDisplay();
                Debug.Log("[AuthHandler] Score actualizado en tiempo real: " + CurrentScore);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[AuthHandler] Error parseando score: " + ex.Message);
            }
        }
    }

    // ─── ADD CALORIA (JUEGO) ────────────────────────────────────────────
    public void AddCaloria()
    {
        CurrentScore++;
        UpdateCaloriasDisplay();
    }

    private void UpdateCaloriasDisplay()
    {
        if (caloriasText)
            caloriasText.text = "Calorías: " + CurrentScore;
    }

    // ─── SEND SCORE (GUARDAR MANUALMENTE) ──────────────────────────────
    public void SendScore()
    {
        if (CurrentUserId == null)
        {
            ShowMessage("No hay usuario autenticado", Color.red);
            return;
        }

        StartCoroutine(SendScoreCoroutine());
    }

    private IEnumerator SendScoreCoroutine()
    {
        ShowMessage("Guardando puntuación...", Color.blue);

        var task = firebaseDatabase.GetReference("users/" + CurrentUserId + "/score")
            .SetValueAsync(CurrentScore);

        yield return new WaitUntil(() => task.IsCompleted);

        if (task.Exception != null)
        {
            ShowMessage("Error al guardar: " + task.Exception.Message, Color.red);
            Debug.LogError("[AuthHandler] Error guardando score: " + task.Exception);
            yield break;
        }

        ShowMessage("¡Puntuación guardada!", Color.green);
        Debug.Log("[AuthHandler] Score guardado: " + CurrentScore);

        // Actualizar leaderboard
        yield return StartCoroutine(GetScoresCoroutine());
    }

    // ─── GET SCORES (LEADERBOARD) ──────────────────────────────────────
    public void GetScores()
    {
        StartCoroutine(GetScoresCoroutine());
    }

    private IEnumerator GetScoresCoroutine()
    {
        ShowMessage("Cargando leaderboard...", Color.blue);

        var task = firebaseDatabase.GetReference("users").GetValueAsync();
        yield return new WaitUntil(() => task.IsCompleted);

        if (task.Exception != null)
        {
            ShowMessage("Error cargando leaderboard", Color.red);
            Debug.LogError("[AuthHandler] Error en leaderboard: " + task.Exception);
            yield break;
        }

        DataSnapshot snapshot = task.Result;
        List<UserScore> users = new List<UserScore>();

        if (snapshot.Exists)
        {
            foreach (DataSnapshot child in snapshot.Children)
            {
                try
                {
                    string username = child.Child("username").Value.ToString();
                    object scoreObj = child.Child("score").Value;
                    int score = scoreObj != null ? int.Parse(scoreObj.ToString()) : 0;

                    users.Add(new UserScore { username = username, score = score });
                }
                catch (System.Exception ex)
                {
                    Debug.LogError("[AuthHandler] Error parseando usuario: " + ex.Message);
                }
            }
        }

        // Ordenar por score descendente
        users.Sort((a, b) => b.score.CompareTo(a.score));

        // Mostrar en UI
        MostrarLeaderboard(users);
        ShowMessage("", Color.white);
    }

    private void MostrarLeaderboard(List<UserScore> users)
    {
        if (leaderboardContainer == null) return;

        // Limpiar items anteriores
        foreach (Transform child in leaderboardContainer)
            Destroy(child.gameObject);

        // Mostrar top 10
        int count = Mathf.Min(10, users.Count);
        for (int i = 0; i < count; i++)
        {
            GameObject item = Instantiate(scoreItemPrefab, leaderboardContainer);
            TMP_Text[] texts = item.GetComponentsInChildren<TMP_Text>();
            
            if (texts.Length >= 2)
            {
                texts[0].text = users[i].username;
                texts[1].text = users[i].score.ToString();
            }
        }

        Debug.Log("[AuthHandler] Leaderboard actualizado: " + count + " usuarios");
    }

    // ─── PASSWORD RECOVERY ─────────────────────────────────────────────
    public void SendPasswordResetEmail()
    {
        string email = recoveryEmailInputField.text.Trim();

        if (string.IsNullOrEmpty(email))
        {
            ShowMessage("Ingrese un email", Color.red);
            return;
        }

        StartCoroutine(SendPasswordResetCoroutine(email));
    }

    private IEnumerator SendPasswordResetCoroutine(string email)
    {
        ShowMessage("Enviando email de recuperación...", Color.blue);

        var task = firebaseAuth.SendPasswordResetEmailAsync(email);
        yield return new WaitUntil(() => task.IsCompleted);

        if (task.Exception != null)
        {
            HandleAuthException(task.Exception, "recovery");
            yield break;
        }

        ShowMessage($"Email enviado a {email}", Color.green);
        yield return new WaitForSeconds(2f);
        ShowLoginPanel();
    }

    // ─── LOGOUT ────────────────────────────────────────────────────────
    public void Logout()
    {
        // Desuscribirse del listener de score
        if (userScoreReference != null)
            userScoreReference.ValueChanged -= HandleScoreChanged;

        // Cerrar sesión en Firebase
        firebaseAuth.SignOut();

        CurrentUserId = null;
        Username = null;
        CurrentScore = 0;

        ShowLoginPanel();
        Debug.Log("[AuthHandler] Sesión cerrada");
    }

    // ─── UI MANAGEMENT ─────────────────────────────────────────────────
    private void SetUIForUserLogged()
    {
        ShowDashboardPanel();
        if (usernameLabel) usernameLabel.text = "Welcome, " + Username;
        UpdateCaloriasDisplay();
        GetScores();
    }

    public void ShowLoginPanel()
    {
        panelLogin?.SetActive(true);
        panelRegister?.SetActive(false);
        panelRecovery?.SetActive(false);
        panelDashboard?.SetActive(false);
    }

    public void ShowRegisterPanel()
    {
        panelLogin?.SetActive(false);
        panelRegister?.SetActive(true);
        panelRecovery?.SetActive(false);
        panelDashboard?.SetActive(false);
    }

    public void ShowRecoveryPanel()
    {
        panelLogin?.SetActive(false);
        panelRegister?.SetActive(false);
        panelRecovery?.SetActive(true);
        panelDashboard?.SetActive(false);
    }

    public void ShowDashboardPanel()
    {
        panelLogin?.SetActive(false);
        panelRegister?.SetActive(false);
        panelRecovery?.SetActive(false);
        panelDashboard?.SetActive(true);
    }

    // ─── ERROR HANDLING ────────────────────────────────────────────────
    private void HandleAuthException(AggregateException exception, string context)
    {
        string errorMessage = "Error";

        foreach (var innerException in exception.InnerExceptions)
        {
            FirebaseException firebaseEx = innerException as FirebaseException;
            if (firebaseEx != null)
            {
                switch ((AuthError)firebaseEx.ErrorCode)
                {
                    case AuthError.InvalidEmail:
                        errorMessage = "Email inválido";
                        break;
                    case AuthError.WrongPassword:
                        errorMessage = "Contraseña incorrecta";
                        break;
                    case AuthError.UserNotFound:
                        errorMessage = "Usuario no encontrado";
                        break;
                    case AuthError.EmailAlreadyInUse:
                        errorMessage = "Este email ya está registrado";
                        break;
                    case AuthError.WeakPassword:
                        errorMessage = "Contraseña muy débil";
                        break;
                    case AuthError.TooManyRequests:
                        errorMessage = "Demasiados intentos, intenta luego";
                        break;
                    default:
                        errorMessage = firebaseEx.Message;
                        break;
                }
            }
        }

        ShowMessage(errorMessage, Color.red);
        Debug.LogError($"[AuthHandler] Error en {context}: {errorMessage}");
    }

    private void ShowMessage(string message, Color color)
    {
        if (statusMessageText)
        {
            statusMessageText.text = message;
            statusMessageText.color = color;
        }
        Debug.Log("[AuthHandler] " + message);
    }

    // ─── CLEANUP ───────────────────────────────────────────────────────
    private void OnDestroy()
    {
        if (firebaseAuth != null)
            firebaseAuth.StateChanged -= AuthStateChanged;

        if (userScoreReference != null)
            userScoreReference.ValueChanged -= HandleScoreChanged;
    }
}

// ─── DATA MODELS ───────────────────────────────────────────────────────
[System.Serializable]
public class UserScore
{
    public string username;
    public int score;
}
