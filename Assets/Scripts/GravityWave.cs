using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GravityWave : MonoBehaviour
{
	[SerializeField]
	private float _amplitude;
	[SerializeField]
	private float _frequency;

	private void Update()
	{
		float g = ((Mathf.Sin(Time.time * _frequency) + 0.5f) / 2f) * _amplitude;
		Physics.gravity = new Vector3(g, Physics.gravity.y, 0f);
	}
}
