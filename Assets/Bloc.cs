using UnityEngine;

public class Bloc : MonoBehaviour
{
    public char typeChar = 'A';
    
    Vector3 deplacement = Vector3.zero;
    
    public void DoAction(Heros heros)
    {
        switch (typeChar)
        {
            case 'E': // Victory!!
            {
                if (Levels.instance.transform.childCount > 0)
                {
                    Levels.instance.currentLevel++;
                    Levels.instance.LoadLevel();
                }

                break;
            }
            case 'K': // Game Over!
            {
                Levels.instance.LoadLevel();
                break;
            }
            case 'B': // Bounce
            {
                heros.Jump();
                break;
            }
            case 'U':
            {
                deplacement = Vector3.up;
                break;
            }
            case 'R':
            {
                deplacement = Vector3.right;
                break;
            }
        }
    }
    
    void FixedUpdate()
    {
        if (deplacement != Vector3.zero)
        {
            transform.position += 0.1f * deplacement;
        }
    }


}
