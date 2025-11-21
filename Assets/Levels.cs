using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
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

    private Heros _localHero;

    private void Awake()
    {
        instance = this;
    }

    public void LoadLevel()
    {
        Debug.Log($"[Levels] LoadLevel level={currentLevel}");

        KillAll();

        try
        {
            int numLevel = currentLevel;
            TextAsset txt = (TextAsset)Resources.Load("level" + numLevel.ToString("00"), typeof(TextAsset));
            CreateLevel(txt.text);
        }
        catch (Exception e)
        {
            string text = System.IO.File.ReadAllText(
                Application.streamingAssetsPath + "/level" + currentLevel.ToString("00") + ".txt"
            );
            CreateLevel(text);
            Debug.Log("Level load error: " + e.Message);
            counting = false;   
        }

        if (_localHero == null)
        {
            Debug.Log("[Levels] Création d'un nouveau héros local.");
            _localHero = Instantiate(herosPrefab, SpawnPosition, Quaternion.identity, this.transform);
        }
        else
        {
            Debug.Log("[Levels] Repositionnement du héros existant.");
            _localHero.transform.position = SpawnPosition;
            var rb = _localHero.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
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
            string levelString = www.downloadHandler.text;
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
                SpawnPosition = position;
                break;
            default:
                Bloc blocPrefab = blocsPrefabs.Find(bloc => bloc.typeChar == blocType);
                if (blocPrefab != null)
                {
                    Bloc bloc = Instantiate(blocPrefab, position, Quaternion.identity, this.transform);
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

        // Très important : on invalide la référence au héros local
        _localHero = null;
        Debug.Log("[Levels] KillAll : enfants détruits, _localHero remis à null.");
    }

    private void Update()
    {
        if (Input.GetKey(KeyCode.F))
        {
            currentLevel = 1;
            LoadLevel();
            currentTime = 0;
            counting = true;
        }
    }

    private void FixedUpdate()
    {
        if (!counting) return;
        currentTime += Time.fixedDeltaTime;
        TimeSpan timeSpan = TimeSpan.FromSeconds(currentTime);
        chronoLabel.text = string.Format("{0:D2}:{1:D2}:{2:D2}",
            timeSpan.Minutes,
            timeSpan.Seconds,
            timeSpan.Milliseconds / 10
        );
    }
}
