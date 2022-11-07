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
	private ComputeBuffer _sortedSpeckCB;
	private ComputeBuffer _partIndicesCB;
	private ComputeBuffer _partCountsCB;
	private ComputeBuffer _pfxSumACB;
	private ComputeBuffer _pfxSumBCB;
	private RenderParams _renderParams;

	private int _updateVelocitiesIdx;
	private int _updatePositionsIdx;
	private int _populatePartitionsIdx;
	private int _initialisePfxSumIdx;
	private int _sumPartCountsIdx;
	private int _clearBuffersIdx;
	private int _sortSpecksIdx;

	private int _partsPerDim;
	private int _numParts;

	private void SetTriangleMesh()
	{
		Vector3[] verts = new Vector3[]
		{
			new Vector3(-2f, -1, 0),
			new Vector3(2f, -1, 0),
			new Vector3(0, 1, 0),
		};

		int[] tris = new int[] { 0, 1, 2 };

		_speckMesh = new Mesh()
		{
			vertices = verts,
			triangles = tris,
		};
	}

    void Start()
    {
		SetTriangleMesh();

		_partsPerDim = Mathf.CeilToInt(_bounds / _maxDist);
		_numParts = _partsPerDim * _partsPerDim * _partsPerDim;
		_bounds = _partsPerDim * _maxDist;
		_computeShader.SetInt("PartsPerDim", _partsPerDim);
		_computeShader.SetInt("NumParts", _numParts);

		_speckCB = new ComputeBuffer(_numSpecks, SPECK_DATA_SIZE);
		_sortedSpeckCB = new ComputeBuffer(_numSpecks, SPECK_DATA_SIZE);
		_partIndicesCB = new ComputeBuffer(_numSpecks, 2 * sizeof(uint));
		_partCountsCB = new ComputeBuffer(_numParts, sizeof(uint));
		_pfxSumACB = new ComputeBuffer(_numParts, sizeof(uint));
		_pfxSumBCB = new ComputeBuffer(_numParts, sizeof(uint));

		Speck[] speckDatas = new Speck[_numSpecks];
		int numPerDim = Mathf.FloorToInt(_bounds / _speckDiameter);
		for (int i = 0; i < _numSpecks; i++)
		{
			float x = (i % numPerDim) * _speckDiameter + (_speckDiameter / 2f);
			float z = ((i / numPerDim) % numPerDim) * _speckDiameter + (_speckDiameter / 2f);
			float y = (i / (numPerDim * numPerDim)) * _speckDiameter + (_speckDiameter / 2f);
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
		_computeShader.SetBuffer(_updateVelocitiesIdx, "SortedSpecks", _sortedSpeckCB);
		_computeShader.SetBuffer(_updateVelocitiesIdx, "PartIndices", _partIndicesCB);
		_computeShader.SetBuffer(_updateVelocitiesIdx, "PartCounts", _partCountsCB);

		_updatePositionsIdx = _computeShader.FindKernel("UpdatePositions");
		_computeShader.SetBuffer(_updatePositionsIdx, "Specks", _speckCB);
		_computeShader.SetBuffer(_updatePositionsIdx, "SortedSpecks", _sortedSpeckCB);
		_computeShader.SetBuffer(_updatePositionsIdx, "PartIndices", _partIndicesCB);
		_computeShader.SetBuffer(_updatePositionsIdx, "PartCounts", _partCountsCB);

		_populatePartitionsIdx = _computeShader.FindKernel("PopulatePartitions");
		_computeShader.SetBuffer(_populatePartitionsIdx, "Specks", _speckCB);
		_computeShader.SetBuffer(_populatePartitionsIdx, "PartIndices", _partIndicesCB);
		_computeShader.SetBuffer(_populatePartitionsIdx, "PartCounts", _partCountsCB);

		_sumPartCountsIdx = _computeShader.FindKernel("SumPartCounts");

		_initialisePfxSumIdx = _computeShader.FindKernel("InitialisePfxSum");
		_computeShader.SetBuffer(_initialisePfxSumIdx, "PartCounts", _partCountsCB);
		_computeShader.SetBuffer(_initialisePfxSumIdx, "PfxSumA", _pfxSumACB);
		_computeShader.SetBuffer(_initialisePfxSumIdx, "PfxSumB", _pfxSumBCB);

		_clearBuffersIdx = _computeShader.FindKernel("ClearBuffers");
		_computeShader.SetBuffer(_clearBuffersIdx, "PartCounts", _partCountsCB);
		_computeShader.SetBuffer(_clearBuffersIdx, "PfxSumA", _pfxSumACB);
		_computeShader.SetBuffer(_clearBuffersIdx, "PfxSumB", _pfxSumBCB);

		_sortSpecksIdx = _computeShader.FindKernel("SortSpecks");
		_computeShader.SetBuffer(_sortSpecksIdx, "PartIndices", _partIndicesCB);
		_computeShader.SetBuffer(_sortSpecksIdx, "PartCounts", _partCountsCB);
		_computeShader.SetBuffer(_sortSpecksIdx, "Specks", _speckCB);
		_computeShader.SetBuffer(_sortSpecksIdx, "SortedSpecks", _sortedSpeckCB);

		_renderParams = new RenderParams(_speckMat);
		_renderParams.matProps = new MaterialPropertyBlock();
		_renderParams.matProps.SetBuffer("Specks", _speckCB);
		_renderParams.matProps.SetBuffer("PartIndices", _partIndicesCB);
		_renderParams.matProps.SetInt("PartsPerDim", _partsPerDim);
		_renderParams.worldBounds = new Bounds(Vector3.zero, Vector3.one * 1000f);
    }

	private void SumPartCounts()
	{
		_computeShader.Dispatch(_initialisePfxSumIdx, Mathf.CeilToInt((float)_numParts / 256), 1, 1);

		// Set prefix sum buffers
		_computeShader.SetBuffer(_sumPartCountsIdx, "PfxSumA", _pfxSumACB);
		_computeShader.SetBuffer(_sumPartCountsIdx, "PfxSumB", _pfxSumBCB);

		bool originalOrder = false;
		for (int n = 1; n < _numParts; n <<= 0)
		{
			_computeShader.SetInt("PfxSumPower", n);
			_computeShader.Dispatch(_sumPartCountsIdx, Mathf.CeilToInt((float)_numParts / 256), 1, 1);


			_computeShader.SetBuffer(_sumPartCountsIdx, "PfxSumA", originalOrder ? _pfxSumACB : _pfxSumBCB);
			_computeShader.SetBuffer(_sumPartCountsIdx, "PfxSumB", originalOrder ? _pfxSumBCB : _pfxSumACB);

			originalOrder = !originalOrder;
		}

		_computeShader.SetBuffer(_updateVelocitiesIdx, "PfxSumB", originalOrder ? _pfxSumBCB : _pfxSumACB);
		_computeShader.SetBuffer(_sortSpecksIdx, "PfxSumB", originalOrder ? _pfxSumBCB : _pfxSumACB);
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
		_computeShader.SetFloat("MaxDist", _maxDist);
		_computeShader.SetFloat("MaxVel", _maxVel);
		_computeShader.SetFloat("BoundsForce", _boundsForce);
		_computeShader.SetFloat("DeltaTime", Time.deltaTime);
		_computeShader.SetVector("Gravity", Physics.gravity);

		_renderParams.matProps.SetFloat("SpeckRadius", _speckDiameter);

		// Clear the buffers
		_computeShader.Dispatch(_clearBuffersIdx, Mathf.CeilToInt((float)_numParts / 256), 1, 1);
		_computeShader.Dispatch(_populatePartitionsIdx, Mathf.CeilToInt((float)_numSpecks / 256), 1, 1);

		SumPartCounts();

		_computeShader.Dispatch(_sortSpecksIdx, Mathf.CeilToInt((float)_numSpecks / 256), 1, 1);
		_computeShader.Dispatch(_updateVelocitiesIdx, Mathf.CeilToInt((float)_numSpecks / 256), 1, 1);
		_computeShader.Dispatch(_updatePositionsIdx, Mathf.CeilToInt((float)_numSpecks / 256), 1, 1);

		Graphics.RenderMeshPrimitives(_renderParams, _speckMesh, 0, _numSpecks);
	}

	private void OnDestroy()
	{
		_speckCB.Release();
		_sortedSpeckCB.Release();
		_partIndicesCB.Release();
		_partCountsCB.Release();
		_pfxSumACB.Release();
		_pfxSumBCB.Release();
	}
}
