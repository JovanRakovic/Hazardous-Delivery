using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class PlanetChunk
{
    public static List<PlanetChunk> chunks;
    public static List<PlanetChunk> visibleChunks;
    public Transform transform;

    public Planet planet;
    /// <summary>
    /// 0/1 - right/left, 
    /// 2/3 - up/down, 
    /// 4/5 - forward/backward, 
    /// </summary>
    public int facingDirection;

    public MeshFilter filter;
    public MeshCollider collider;

    public int currentTreeDepth;

    public bool hasParent = false;
    public PlanetChunk parent;

    public bool hasChildern = false;
    public int childrenToDestroy = 0;
    public PlanetChunk[] children;
    public bool destructionRequested = false;

    public bool hasMesh = false;

    private Mesh mesh = null;

    public Vector2 center = Vector2.zero;
    public Vector3 boundCenter = Vector2.zero;

    public PlanetChunk(Planet _planet, int _facingDirection, Transform _transform, int _currentDepth, PlanetChunk _parent, Vector2 _center)
    {
        planet = _planet;
        facingDirection = _facingDirection;
        transform = _transform;
        currentTreeDepth = _currentDepth;
        hasParent = true;
        parent = _parent;
        center = _center;

        Start();
    }

    public PlanetChunk(Planet _planet, int _facingDirection, Transform _transform, int _currentDepth, Vector2 _center)
    {
        planet = _planet;
        facingDirection = _facingDirection;
        transform = _transform;
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

    public void AssignChildren(PlanetChunk[] _children) 
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

    public void SetChildrenStatus(bool toggle) 
    {
        hasChildern = toggle;
    }

    void Start()
    {
        filter = transform.gameObject.GetComponent<MeshFilter>();
        collider = transform.gameObject.GetComponent<MeshCollider>();

        Renderer renderer = transform.gameObject.GetComponent<Renderer>();
        renderer.sharedMaterial = planet.generalSettings.mat;

        mesh = new Mesh();

        if (chunks == null)
            chunks = new List<PlanetChunk>();

        if(visibleChunks == null)
            visibleChunks = new List<PlanetChunk>();

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

    public void RequestRegenerate() 
    {
        if (hasChildern) 
        {
            foreach (PlanetChunk c in children) 
                c.RequestRegenerate();
            return;
        }

        hasMesh = false;
    }
}