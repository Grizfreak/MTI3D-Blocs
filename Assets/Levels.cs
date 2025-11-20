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
    
    // url des fichiers niveaux
    private string baseUrl = "https://www.zerokcm.fr/blocs/levels/";

    [SerializeField] private TextMeshProUGUI chronoLabel;
    
    public float currentTime = 0;
    
    public bool counting = true;

    private void Awake()
    {
        instance = this;
        chronoLabel.text = baseUrl + currentLevel.ToString("00");
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
            Debug.Log("Level load error: " + e.Message);
            counting = false;   
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
                Heros heros = Instantiate<Heros>(herosPrefab, this.transform);
                heros.transform.position = position;
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

    private void FixedUpdate()
    {
        if (Input.GetKey(KeyCode.F))
        {
            instance.currentLevel = 1;
            instance.KillAll();
            instance.LoadLevel();
            instance.currentTime = 0;
            instance.counting = true;
            return;
        }
        if (!counting) return;
        currentTime += Time.fixedDeltaTime;
        TimeSpan timeSpan = TimeSpan.FromSeconds(currentTime);
        chronoLabel.text = string.Format("{0:D2}:{1:D2}:{2:D2}", timeSpan.Minutes, timeSpan.Seconds, timeSpan.Milliseconds / 10);
    }
}
