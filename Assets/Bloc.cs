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
                    Levels.instance.SetCoins(Levels.instance.GetCoins() + Levels.instance.GetCoinsCollectedInLevel());
                    Levels.instance.SetCoinsCollectedInLevel(0);
                    Levels.instance.LoadLevel();
                }

                break;
            }
            case 'K': // Game Over!
            {
                Levels.instance.SetCoinsCollectedInLevel(0);
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
            case 'P':
            {
                transform.rotation = Quaternion.Euler(0, 90, 90);
                Levels.instance.SetCoinsCollectedInLevel(Levels.instance.GetCoinsCollectedInLevel() + 1);
                Destroy(this.gameObject);
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
