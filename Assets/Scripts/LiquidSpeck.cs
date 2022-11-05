using UnityEngine;

public class LiquidSpeck : MonoBehaviour
{
	[SerializeField]
	private LiquidSpeckConfig _config;
	private Vector3 _vel;

	public void Update()
	{
		Vector3 force = Vector3.zero;
		foreach (LiquidSpeck speck in LiquidManager.Specks)
		{
			if (speck == this)
				continue;

			float forceMag = CalculateAtrraction(speck.transform) - CalculateRepulsion(speck.transform);
			Vector3 displacement = speck.transform.position - transform.position;
			if (displacement.sqrMagnitude == 0)
			{
				displacement = Random.onUnitSphere;
			}
			force += displacement.normalized * forceMag * _config.Multiplier;
		}

		Vector3 a = force / _config.Mass;

		a += Physics.gravity;
		_vel += a * Time.deltaTime;
		_vel *= 1f - _config.Damper;
	}

	private void ClampInBounds(Vector3 axis, float bound)
	{
		axis.Normalize();
		float pos = Vector3.Dot(transform.position, axis);
		float sign = Mathf.Sign(pos);
		float mag = Mathf.Abs(pos);
		if (mag > bound)
		{
			transform.position = Vector3.ProjectOnPlane(transform.position, axis) + axis * bound * sign;
			_vel = Vector3.ProjectOnPlane(_vel, axis);
		}
	}

	public void LateUpdate()
	{
		transform.position += _vel * Time.deltaTime;

		if (transform.position.y < 0)
		{
			transform.position = new Vector3(transform.position.x, 0f, transform.position.z);
			_vel = new Vector3(_vel.x, 0f, _vel.z);
		}

		// Wall repulsion
		ClampInBounds(Vector3.right, LiquidManager.Bounds);
		ClampInBounds(Vector3.forward, LiquidManager.Bounds);
	}

	private float CalculateAtrraction(Transform t)
	{
		float x = Vector3.Distance(transform.position, t.position) - transform.localScale.x;
		x = Mathf.Max(x, 0);
		return _config.AttractionC * Mathf.Exp((x + _config.AttractionE) / -_config.AttractionD);
	}

	private float CalculateRepulsion(Transform t)
	{
		float x = Vector3.Distance(transform.position, t.position) - transform.localScale.x;
		x = Mathf.Max(x, 0);
		return _config.RepulsionA * Mathf.Exp(-x / _config.RepulsionB);
	}
}
