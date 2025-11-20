using UnityEngine;

public class Heros : MonoBehaviour
{
    Vector2 acceleration = Vector2.zero;
    float previousJump = 0;
    Rigidbody rigidbody;
    
    void Start()
    {
        rigidbody = GetComponent<Rigidbody>();
    }

    void OnCollisionEnter(Collision collision)
    {
    // Lorsque le heros percute un bloc, on lance l’action du bloc
        if (collision.gameObject.TryGetComponent(out Bloc bloc))
        {
            bloc.DoAction(this);
        }

    }

    public void Jump()
    {
        if (Time.time - previousJump > 0.2f)
        {
            previousJump = Time.time;
            GetComponent<Rigidbody>().AddForce(15.0f * Vector3.up, ForceMode.Impulse);
        }
    }
    
    void FixedUpdate()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            Levels.instance.KillAll();
            Levels.instance.LoadLevel();
            return;
        }
        if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A))
        {
            acceleration.x -= 0.2f;
        }
        if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D))
        {
            acceleration.x += 0.2f;
        }
        acceleration *= 0.8f;

        if (rigidbody.linearVelocity.magnitude > 20.0f)
        {
            rigidbody.linearVelocity = 20.0f * rigidbody.linearVelocity.normalized;
        }
        rigidbody.AddForce(100.0f * acceleration, ForceMode.Force);
        
        //lerp camera to follow heros
        Camera.main.transform.position = Vector3.Lerp(Camera.main.transform.position, new Vector3(transform.position.x, transform.position.y, Camera.main.transform.position.z), 0.1f);
    }


}
