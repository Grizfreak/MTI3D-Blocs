using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Networking;

public class Levels : MonoBehaviour
{
    //Singleton
    public static Levels instance;
    
    // Liste des blocs disponibles
    public List<Bloc> blocsPrefabs;
    
    //HerosPrefab
    public Heros herosPrefab;
    
    // level en cours
    public int currentLevel = 1;

    [SerializeField] private TextMeshProUGUI chronoLabel;
    
    public float currentTime = 0;
    
    public bool counting = true;
    
    public Vector3 SpawnPosition { get; private set; } = Vector3.zero;

    private void Awake()
    {
        instance = this;
    }

    public void LoadLevel()
    {
        try
        {
            int numLevel = Levels.instance.currentLevel;
            {
                TextAsset txt = (TextAsset)Resources.Load("level" + numLevel.ToString("00"), typeof(TextAsset));
                CreateLevel(txt.text);
            }
        } catch (Exception e)
        {
            // Search for new levels in StreamingAssets
            string text = System.IO.File.ReadAllText(Application.streamingAssetsPath + "/level" + Levels.instance.currentLevel.ToString("00") + ".txt");
            CreateLevel(text);
            Debug.Log("Level load error: " + e.Message);
            counting = false;   
        }
        
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            var localClient = NetworkManager.Singleton.LocalClient;
            if (localClient != null && localClient.PlayerObject != null)
            {
                Transform playerTransform = localClient.PlayerObject.transform;
                playerTransform.position = SpawnPosition;

                // Reset physique
                var rb = playerTransform.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector2.zero;
                    rb.angularVelocity = 0f;
                }

                // Si tu veux être clean : réactiver la physique ici
                var heros = playerTransform.GetComponent<Heros>();
                if (heros != null)
                {
                    // ex: heros.EnablePhysics(); si tu as cette méthode
                }
            }
        }
        else
        {
            // Mode solo : on crée le héros maintenant que la map existe
            Heros heros = Instantiate<Heros>(herosPrefab, this.transform);
            heros.transform.position = SpawnPosition;
        }
    }

    IEnumerator DownloadLevel(string url)
    {
        UnityWebRequest www = UnityWebRequest.Get(url);
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.Log(www.error);
        }
        else
        {
            //assign string
            string levelString = www.downloadHandler.text;
            
            // generate now
            CreateLevel(levelString);
        }
    }

    void CreateLevel(string levelString)
    {
        int x = 0;
        int y = 0;
        foreach (char c in levelString)
        {
            switch(c)
            {
                case '\n':
                    y--;
                    x = 0;
                    break;
                case ' ':
                    x++;
                    break;
                default:
                    CreateBloc(c, new Vector3(x, y, 0));
                    x++;
                    break;
            }
        }
    }

    void CreateBloc(char blocType, Vector3 position)
    {
        switch (blocType)
        {
            case 'S':
                //Heros
                SpawnPosition = position;
                break;
            default:
                Bloc blocPrefab = blocsPrefabs.Find(bloc => bloc.typeChar == blocType);
                if (blocPrefab != null)
                {
                    Bloc bloc = Instantiate<Bloc>(blocPrefab, this.transform);
                    bloc.transform.position = position;
                }
                break;
        }
    }
    
    public void KillAll()
    {
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }
    }

    private void Update()
    {
        if (Input.GetKey(KeyCode.F))
        {
            instance.currentLevel = 1;
            instance.KillAll();
            instance.LoadLevel();
            instance.currentTime = 0;
            instance.counting = true;
        }
    }

    private void FixedUpdate()
    {
        if (!counting) return;
        currentTime += Time.fixedDeltaTime;
        TimeSpan timeSpan = TimeSpan.FromSeconds(currentTime);
        chronoLabel.text = string.Format("{0:D2}:{1:D2}:{2:D2}", timeSpan.Minutes, timeSpan.Seconds, timeSpan.Milliseconds / 10);
    }
}
