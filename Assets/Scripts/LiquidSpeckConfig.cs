using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Scriptable Objects/Liquid Speck Config")]
public class LiquidSpeckConfig : ScriptableObject
{
	public float RepulsionA;
	public float RepulsionB;
	public float RepulsionF;

	public float AttractionC;
	public float AttractionD;
	public float AttractionE;

	public float Multiplier;
	[Range(0, 1)]
	public float Damper;
}
