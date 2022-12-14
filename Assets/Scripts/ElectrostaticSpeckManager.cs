using UnityEngine;

struct Speck
{
	public Vector3 Pos;
	public Vector3 Vel;
	public int D;
}

public class ElectroStaticSpeckManager : MonoBehaviour
{
	private const int SPECK_DATA_SIZE = (sizeof(float) * 6) + sizeof(int);

	[SerializeField]
	private int _numSpecks;
	[SerializeField]
	private float _speckDiameter = 0.1f;
	[SerializeField]
	private ElectrostaticConfig _config;
	[SerializeField]
	private ComputeShader _computeShader;

	[SerializeField]
	private Material _speckMat;
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
	private int _sumPartCountsIdx;
	private int _clearBuffersIdx;
	private int _sortSpecksIdx;

	private int _partsPerDim;
	private int _numParts;
	private float _bounds;

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

		_partsPerDim = Mathf.CeilToInt(_config.BoundaryWidth / _config.MaxDist);
		_numParts = _partsPerDim * _partsPerDim * _partsPerDim;
		_bounds = _partsPerDim * _config.MaxDist;
		_computeShader.SetInt("PartsPerDim", _partsPerDim);
		_computeShader.SetInt("NumParts", _numParts);
		_computeShader.SetInt("NumSpecks", _numSpecks);

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
				D = 0,
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
		_computeShader.SetBuffer(_populatePartitionsIdx, "PfxSumA", _pfxSumACB);

		_sumPartCountsIdx = _computeShader.FindKernel("SumPartCounts");

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
		// Set prefix sum buffers
		_computeShader.SetBuffer(_sumPartCountsIdx, "PfxSumA", _pfxSumACB);
		_computeShader.SetBuffer(_sumPartCountsIdx, "PfxSumB", _pfxSumBCB);

		int i = 0;
		for (int n = 1; n < _numParts; i++)
		{
			_computeShader.SetInt("PfxSumPower", n);
			_computeShader.Dispatch(_sumPartCountsIdx, Mathf.CeilToInt((float)_numParts / 256), 1, 1);

			_computeShader.SetBuffer(_sumPartCountsIdx, "PfxSumA", (i & 1) == 0 ? _pfxSumBCB : _pfxSumACB);
			_computeShader.SetBuffer(_sumPartCountsIdx, "PfxSumB", (i & 1) == 0 ? _pfxSumACB : _pfxSumBCB);

			n <<= 1;
		}

		_computeShader.SetBuffer(_updateVelocitiesIdx, "PfxSumB", (i & 1) == 0 ? _pfxSumACB : _pfxSumBCB);
		_computeShader.SetBuffer(_sortSpecksIdx, "PfxSumB", (i & 1) == 0 ? _pfxSumACB : _pfxSumBCB);
	}

	public void Update()
	{
		_computeShader.SetFloat("K", _config.K);
		_computeShader.SetFloat("A", _config.A);
		_computeShader.SetFloat("B", _config.B);
		_computeShader.SetFloat("C", _config.C);
		_computeShader.SetFloat("D", _config.D);
		_computeShader.SetFloat("E", _config.E);
		_computeShader.SetFloat("F", _config.F);
		_computeShader.SetFloat("Multiplier", _config.Multiplier);

		_computeShader.SetFloat("KBoundary", _config.KBoundary);
		_computeShader.SetFloat("Restitution", _config.Restitution);

		_computeShader.SetFloat("SpeckRadius", _speckDiameter);
		_computeShader.SetFloat("Damper", _config.Damper);
		_computeShader.SetFloat("Bounds", _bounds);
		_computeShader.SetFloat("MaxDist", _config.MaxDist);
		_computeShader.SetFloat("MaxVel", _config.MaxVel);
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
