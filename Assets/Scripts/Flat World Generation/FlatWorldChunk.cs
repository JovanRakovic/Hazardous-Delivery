using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlatWorldChunk
{
    public static List<FlatWorldChunk> chunks;
    public static List<FlatWorldChunk> visibleChunks;
    public Transform transform;

    [Range(4, 255)]
    public int resolution;

    public MeshFilter filter;
    public MeshCollider collider;

    public float distance = 10;
    public int maxTreeDepth;
    public int currentTreeDepth;

    public bool hasParent = false;
    public FlatWorldChunk parent;

    public bool hasChildern = false;
    public int childrenToDestroy = 0;
    public FlatWorldChunk[] children;
    public bool destructionRequested = false;

    public bool hasMesh = false;

    private Mesh mesh = null;

    public Vector2 center = Vector2.zero;
    public Vector3 boundCenter = Vector2.zero;

    public FlatWorldChunk(Transform _transform, int _resolution, float _distance, int _maxDepth, int _currentDepth, FlatWorldChunk _parent, Vector2 _center)
    {
        transform = _transform;
        resolution = _resolution;
        distance = _distance;
        maxTreeDepth = _maxDepth;
        currentTreeDepth = _currentDepth;
        hasParent = true;
        parent = _parent;
        center = _center;

        Start();
    }

    public FlatWorldChunk(Transform _transform, int _resolution, float _distance, int _maxDepth, int _currentDepth, Vector2 _center)
    {
        transform = _transform;
        resolution = _resolution;
        distance = _distance;
        maxTreeDepth = _maxDepth;
        currentTreeDepth = _currentDepth;
        hasParent = false;
        center = _center;

        Start();
    }

    public void EstimateBound(float height) 
    {
        boundCenter = new Vector3(center.x * 2.5f, height, center.y * 2.5f);
    }

    public void SetMesh(Mesh _mesh) 
    {
        if(filter == null || collider == null)
            return;

        mesh.Clear();

        mesh = _mesh;
        //mesh.RecalculateNormals();
        hasMesh = true;

        filter.sharedMesh = mesh;
        collider.sharedMesh = mesh;

        boundCenter = mesh.bounds.center;
    }

    public Mesh GetMesh()
    {
        return mesh;
    }

    public void ClearMesh() 
    {
        filter.sharedMesh = null;
        collider.sharedMesh = null;
        hasMesh = false;
        mesh.Clear();
    }

    public void AssignChildren(FlatWorldChunk[] _children) 
    {
        visibleChunks.Remove(this);
        children = _children;
        hasChildern = true;
    }

    public void RequestDestruction() 
    {
        parent.childrenToDestroy++;
        destructionRequested = true;
        visibleChunks.Remove(this);
    }

    public void VoidDestructionRequest() 
    {
        parent.childrenToDestroy--;
        destructionRequested = false;
        visibleChunks.Add(this);
    }

    public void RenounceChildren() 
    {
        childrenToDestroy = 0;
        hasChildern = false;
        visibleChunks.Add(this);
    }

    public void VoidChildren() 
    {
        children = null;
        hasChildern = false;
    }

    void Start()
    {
        filter = transform.gameObject.GetComponent<MeshFilter>();
        collider = transform.gameObject.GetComponent<MeshCollider>();

        mesh = new Mesh();

        if (chunks == null)
            chunks = new List<FlatWorldChunk>();

        if(visibleChunks == null)
            visibleChunks = new List<FlatWorldChunk>();

        chunks.Add(this);
        visibleChunks.Add(this);
    }

    public void OnDestroy() 
    {
        mesh.Clear();
        GameObject.Destroy(transform.gameObject);
        transform = null;
        chunks.Remove(this);
        visibleChunks.Remove(this);
    }
}