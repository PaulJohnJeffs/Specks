using System.Collections;
using System.Collections.Generic;
using UnityEngine;

struct Speck
{
	Vector3 Pos;
	Vector3 Vel;
}

public class LiquidManager : MonoBehaviour
{
	[SerializeField]
	private int _numSpecks;

	public static float Bounds => _bounds;
	[SerializeField]
	private static float _bounds = 1f;
	[SerializeField]
	private GameObject _speckPrefab;

	public static LiquidSpeck[] Specks => _specks;

	private static LiquidSpeck[] _specks;

    void Start()
    {
		_specks = new LiquidSpeck[_numSpecks];

		for (int i = 0; i < _numSpecks; i++)
		{
			Vector3 pos = new Vector3(Random.value - 0.5f, Random.value - 0.5f, Random.value - 0.5f) * _bounds;
			GameObject go = Instantiate(_speckPrefab, pos, Quaternion.identity, transform);
			LiquidSpeck speck = go.GetComponent<LiquidSpeck>();
			_specks[i] = speck;
		}
    }
}
