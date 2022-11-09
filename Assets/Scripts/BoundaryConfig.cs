using UnityEngine;

[CreateAssetMenu(menuName = "Scriptable Objects/Boundary Config")]
public class BoundaryConfig : ScriptableObject
{
	public float K;
	public float Phase = 0;
	public float Amplitude = 1;
	public float Wavelength = 1;
	[Range (0, 1)]
	public float Restitution = 0.2f;
}
