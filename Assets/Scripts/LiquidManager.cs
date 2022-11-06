using System.Collections;
using System.Collections.Generic;
using UnityEngine;

struct Speck
{
	public Vector3 Pos;
	public Vector3 Vel;
}

public class LiquidManager : MonoBehaviour
{
	private const int SPECK_DATA_SIZE = sizeof(float) * 6;

	[SerializeField]
	private int _numSpecks;
	[SerializeField]
	private float _speckDiameter = 0.1f;
	[SerializeField]
	private float _bounds = 1f;
	[SerializeField]
	private float _boundsForce = 5f;
	[SerializeField]
	private float _maxVel;
	[SerializeField]
	private float _maxDist;
	[SerializeField]
	private LiquidSpeckConfig _config;
	[SerializeField]
	private ComputeShader _computeShader;

	[SerializeField]
	private Material _speckMat;
	[SerializeField]
	private Mesh _speckMesh;

	private ComputeBuffer _speckCB;
	private int _updateVelocitiesIdx;
	private int _updatePositionsIdx;

	private RenderParams _renderParams;

    void Start()
    {
		_speckCB = new ComputeBuffer(_numSpecks, SPECK_DATA_SIZE);
		//_sortedSpeckCB = new ComputeBuffer(_numSpecks, SPECK_DATA_SIZE);

		Speck[] speckDatas = new Speck[_numSpecks];
		int numPerDim = Mathf.FloorToInt(_bounds / _speckDiameter);
		for (int i = 0; i < _numSpecks; i++)
		{
			float x = (i % numPerDim) * _speckDiameter + (_speckDiameter / 2f);
			float y = ((i / numPerDim) % numPerDim) * _speckDiameter + (_speckDiameter / 2f);
			float z = (i / (numPerDim * numPerDim)) * _speckDiameter + (_speckDiameter / 2f);
			//Vector3 pos = new Vector3(Random.value - 0.5f, Random.value - 0.5f, Random.value - 0.5f) * _bounds;
			Vector3 pos = new Vector3(x, y, z) - (new Vector3(1, 1, 1) * (_bounds / 2));
			Speck speck = new Speck()
			{
				Pos = pos,
				Vel = Vector3.zero,
			};

			speckDatas[i] = speck;
		}

		_speckCB.SetData(speckDatas);

		_updateVelocitiesIdx = _computeShader.FindKernel("UpdateVelocities");
		_computeShader.SetBuffer(_updateVelocitiesIdx, "Specks", _speckCB);

		_updatePositionsIdx = _computeShader.FindKernel("UpdatePositions");
		_computeShader.SetBuffer(_updatePositionsIdx, "Specks", _speckCB);

		_renderParams = new RenderParams(_speckMat);
		_renderParams.matProps = new MaterialPropertyBlock();
		_renderParams.matProps.SetBuffer("Specks", _speckCB);
		_renderParams.worldBounds = new Bounds(Vector3.zero, Vector3.one * 1000f);
    }

	public void Update()
	{
		_computeShader.SetFloat("A", _config.RepulsionA);
		_computeShader.SetFloat("B", _config.RepulsionB);
		_computeShader.SetFloat("C", _config.AttractionC);
		_computeShader.SetFloat("D", _config.AttractionD);
		_computeShader.SetFloat("E", _config.AttractionE);
		_computeShader.SetFloat("F", _config.RepulsionF);
		_computeShader.SetFloat("Multiplier", _config.Multiplier);

		_computeShader.SetInt("NumSpecks", _numSpecks);
		_computeShader.SetFloat("SpeckRadius", _speckDiameter);
		_computeShader.SetFloat("Damper", _config.Damper);
		_computeShader.SetFloat("Bounds", _bounds);
		_computeShader.SetFloat("MaxVel", _maxVel);
		_computeShader.SetFloat("MaxDist", _maxDist);
		_computeShader.SetFloat("BoundsForce", _boundsForce);
		_computeShader.SetFloat("DeltaTime", Time.deltaTime);
		_computeShader.SetVector("Gravity", Physics.gravity);

		_renderParams.matProps.SetFloat("SpeckRadius", _speckDiameter);

		_computeShader.Dispatch(_updateVelocitiesIdx, Mathf.CeilToInt((float)_numSpecks / 256), 1, 1);
		_computeShader.Dispatch(_updatePositionsIdx, Mathf.CeilToInt((float)_numSpecks / 256), 1, 1);

		Speck[] read = new Speck[_numSpecks];
		_speckCB.GetData(read);

		Graphics.RenderMeshPrimitives(_renderParams, _speckMesh, 0, _numSpecks);
	}

	private void OnDestroy()
	{
		_speckCB.Release();
	}
}
