using UnityEngine;

/// <summary>Art-preview trajectory only; contains no collision, inventory or combat effects.</summary>
public sealed class UrbanPreviewThrownProp : MonoBehaviour
{
    private Vector3 _start,_velocity;
    private float _elapsed;
    public void Launch(Vector3 velocity){_start=transform.position;_velocity=velocity;}
    private void Update()
    {
        _elapsed+=Time.deltaTime;
        transform.position=_start+_velocity*_elapsed+Physics.gravity*(_elapsed*_elapsed*.5f);
        transform.Rotate(Vector3.forward,300*Time.deltaTime,Space.World);
        if(_elapsed>1.4f)Destroy(gameObject);
    }
}
