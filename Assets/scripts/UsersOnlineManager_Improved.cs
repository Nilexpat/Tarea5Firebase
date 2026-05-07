using Firebase.Auth;
using Firebase.Database;
using System;
using UnityEngine;

/// <summary>
/// UsersOnlineManager: Maneja el estado online/offline de usuarios en tiempo real
/// Basado en el código de tu profe pero integrado con el nuevo AuthHandler
/// </summary>
public class UsersOnlineManager : MonoBehaviour
{
    private FirebaseDatabase firebaseDatabase;
    private DatabaseReference usersOnlineReference;
    private string CurrentUserId;

    // ─── UI PARA MOSTRAR USUARIOS ONLINE ──────────────────────────────
    [SerializeField] private Transform usersOnlineContainer;
    [SerializeField] private GameObject userOnlineItemPrefab;

    private void Start()
    {
        InitializeFirebase();
        SetupAuthListener();
    }

    // ─── INITIALIZE ────────────────────────────────────────────────────
    private void InitializeFirebase()
    {
        firebaseDatabase = FirebaseDatabase.DefaultInstance;
        usersOnlineReference = firebaseDatabase.GetReference("users-online");

        Debug.Log("[UsersOnlineManager] Inicializado");
    }

    // ─── AUTH LISTENER ──────────────────────────────────────────────────
    /// <summary>
    /// Detecta cuando el usuario inicia o cierra sesión
    /// </summary>
    private void SetupAuthListener()
    {
        FirebaseAuth.DefaultInstance.StateChanged += AuthStateChanged;
    }

    private void AuthStateChanged(object sender, EventArgs e)
    {
        FirebaseAuth auth = FirebaseAuth.DefaultInstance;

        if (auth.CurrentUser != null)
        {
            // Usuario inició sesión
            CurrentUserId = auth.CurrentUser.UserId;
            SetUserOnline();
            ListenForUsersOnline();

            Debug.Log("[UsersOnlineManager] Usuario online: " + CurrentUserId);
        }
        else
        {
            // Usuario cerró sesión
            if (CurrentUserId != null)
                SetUserOffline();

            CurrentUserId = null;
            ClearUsersOnlineUI();
        }
    }

    // ─── SET USER ONLINE ───────────────────────────────────────────────
    /// <summary>
    /// Marca el usuario como online en Realtime Database
    /// Se ejecuta cuando inicia sesión
    /// </summary>
    private void SetUserOnline()
    {
        if (CurrentUserId == null) return;

        // Obtener username del usuario actual
        var task = firebaseDatabase.GetReference("users/" + CurrentUserId + "/username").GetValueAsync();
        
        task.ContinueWith(t =>
        {
            if (t.IsCompleted && !t.IsFaulted && t.Result.Exists)
            {
                string username = t.Result.Value.ToString();
                
                // Guardar en users-online
                usersOnlineReference.Child(CurrentUserId).SetValueAsync(new { username = username });

                Debug.Log("[UsersOnlineManager] Marcado como online: " + username);
            }
        });
    }

    // ─── SET USER OFFLINE ──────────────────────────────────────────────
    /// <summary>
    /// Marca el usuario como offline eliminando entrada de Realtime Database
    /// Se ejecuta cuando cierra sesión
    /// </summary>
    private void SetUserOffline()
    {
        if (CurrentUserId == null) return;

        usersOnlineReference.Child(CurrentUserId).SetValueAsync(null);
        Debug.Log("[UsersOnlineManager] Marcado como offline: " + CurrentUserId);
    }

    // ─── LISTEN FOR USERS ONLINE ───────────────────────────────────────
    /// <summary>
    /// Configura listeners en tiempo real para detectar usuarios que entran/salen
    /// </summary>
    private void ListenForUsersOnline()
    {
        if (usersOnlineReference == null) return;

        // Listener para cuando un usuario entra online
        usersOnlineReference.ChildAdded += HandleChildAdded;

        // Listener para cuando un usuario sale (desconexión)
        usersOnlineReference.ChildRemoved += HandleChildRemoved;

        // Cargar usuarios actuales
        usersOnlineReference.GetValueAsync().ContinueWith(task =>
        {
            if (task.IsCompleted && task.Result.Exists)
            {
                foreach (DataSnapshot child in task.Result.Children)
                {
                    Debug.Log("[UsersOnlineManager] Usuario online: " + child.Key);
                }
            }
        });

        Debug.Log("[UsersOnlineManager] Listeners configurados");
    }

    // ─── HANDLE CHILD ADDED (Usuario entra) ────────────────────────────
    private void HandleChildAdded(object sender, ChildChangedEventArgs args)
    {
        DataSnapshot snapshot = args.Snapshot;
        
        try
        {
            string userId = snapshot.Key;
            string username = snapshot.Child("username").Value.ToString();

            Debug.Log("[UsersOnlineManager] ✓ Usuario online: " + username);

            // Actualizar UI
            AddUserToOnlineList(userId, username);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[UsersOnlineManager] Error en HandleChildAdded: " + ex.Message);
        }
    }

    // ─── HANDLE CHILD REMOVED (Usuario sale) ───────────────────────────
    private void HandleChildRemoved(object sender, ChildChangedEventArgs args)
    {
        DataSnapshot snapshot = args.Snapshot;
        
        try
        {
            string userId = snapshot.Key;
            string username = snapshot.Child("username").Value?.ToString() ?? userId;

            Debug.Log("[UsersOnlineManager] ✗ Usuario offline: " + username);

            // Actualizar UI
            RemoveUserFromOnlineList(userId);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[UsersOnlineManager] Error en HandleChildRemoved: " + ex.Message);
        }
    }

    // ─── UI METHODS ─────────────────────────────────────────────────────
    
    private void AddUserToOnlineList(string userId, string username)
    {
        if (usersOnlineContainer == null || userOnlineItemPrefab == null)
            return;

        // Verificar si el usuario ya está en la lista
        Transform existing = usersOnlineContainer.Find(userId);
        if (existing != null)
            return; // Ya existe

        // Crear nuevo item
        GameObject item = Instantiate(userOnlineItemPrefab, usersOnlineContainer);
        item.name = userId;

        TMPro.TextMeshProUGUI text = item.GetComponent<TMPro.TextMeshProUGUI>();
        if (text)
            text.text = username + " (online)";

        Debug.Log("[UsersOnlineManager] Usuario agregado a UI: " + username);
    }

    private void RemoveUserFromOnlineList(string userId)
    {
        if (usersOnlineContainer == null)
            return;

        Transform userItem = usersOnlineContainer.Find(userId);
        if (userItem != null)
        {
            Destroy(userItem.gameObject);
            Debug.Log("[UsersOnlineManager] Usuario removido de UI: " + userId);
        }
    }

    private void ClearUsersOnlineUI()
    {
        if (usersOnlineContainer == null)
            return;

        foreach (Transform child in usersOnlineContainer)
            Destroy(child.gameObject);

        Debug.Log("[UsersOnlineManager] Lista de online limpiada");
    }

    // ─── GET ONLINE COUNT ──────────────────────────────────────────────
    /// <summary>
    /// Retorna la cantidad de usuarios online actualmente
    /// </summary>
    public void GetOnlineCount()
    {
        usersOnlineReference.GetValueAsync().ContinueWith(task =>
        {
            if (task.IsCompleted && task.Result.Exists)
            {
                int count = (int)task.Result.ChildrenCount;
                Debug.Log("[UsersOnlineManager] Usuarios online: " + count);
            }
        });
    }

    // ─── CLEANUP ────────────────────────────────────────────────────────
    private void OnDestroy()
    {
        // Desuscribirse de listeners
        if (usersOnlineReference != null)
        {
            usersOnlineReference.ChildAdded -= HandleChildAdded;
            usersOnlineReference.ChildRemoved -= HandleChildRemoved;
        }

        // Marcar como offline si estaba online
        if (CurrentUserId != null)
            SetUserOffline();

        // Desuscribirse del listener de autenticación
        if (FirebaseAuth.DefaultInstance != null)
            FirebaseAuth.DefaultInstance.StateChanged -= AuthStateChanged;
    }
}
