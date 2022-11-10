using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Scriptable Objects/Electrostatic Config")]
public class ElectrostaticConfig : ScriptableObject
{
	[Header ("Particle-Particle Forces")]
	public float K;
	public float A;
	public float B;
	public float C;
	public float D;
	public float E;
	public float F;
	public float Multiplier;

	[Space]
	[Header ("Boundary")]
	public float BoundaryWidth = 1f;
	public float KBoundary;
	[Range(0, 1)]
	public float Restitution = 0.2f;

	[Space]
	[Header ("Kinematics")]
	[Range(0, 1)]
	public float Damper;
	public float MaxVel;
	public float MaxDist;
}
