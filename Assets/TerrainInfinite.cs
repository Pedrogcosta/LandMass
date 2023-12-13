using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TerrainInfinite : MonoBehaviour
{

    const float scale = 2.5f;

    const float MoveLimit = 25f;
    const float sqrMoveLimit = MoveLimit * MoveLimit;

    public LODInfo[] LOD;
    public static float maxFOV;

    public Transform viewer;
    public Material mapMaterial;

    public static Vector2 viewerPosition;
    Vector2 viewerPositionOld;
    static MapGenerator mapGenerator;
    int chunkSize;
    int ChunksinFOV;

    public Object caveEntrance;

    Dictionary<Vector2, TerrainChunk> terrainChunkDictionary = new Dictionary<Vector2, TerrainChunk>();
    static List<TerrainChunk> chunksLastUpdate = new List<TerrainChunk>();

    void Start()
    {
        mapGenerator = FindObjectOfType<MapGenerator>();

        maxFOV = LOD[LOD.Length - 1].visibleDistance;
        chunkSize = MapGenerator.ChunkSize - 1;
        ChunksinFOV = Mathf.RoundToInt(maxFOV / chunkSize);

        UpdateChunks();
    }

    void Update()
    {
        viewerPosition = new Vector2(viewer.position.x, viewer.position.z) / scale;

        if ((viewerPositionOld - viewerPosition).sqrMagnitude > sqrMoveLimit)
        {
            viewerPositionOld = viewerPosition;
            UpdateChunks();
        }
    }

    void UpdateChunks()
    {

        for (int i = 0; i < chunksLastUpdate.Count; i++)
        {
            chunksLastUpdate[i].SetVisible(false);
        }
        chunksLastUpdate.Clear();

        int chunkcoordx = Mathf.RoundToInt(viewerPosition.x / chunkSize);
        int chunkcoordy = Mathf.RoundToInt(viewerPosition.y / chunkSize);

        for (int yOffset = -ChunksinFOV; yOffset <= ChunksinFOV; yOffset++)
        {
            for (int xOffset = -ChunksinFOV; xOffset <= ChunksinFOV; xOffset++)
            {
                Vector2 viewedChunkCoord = new Vector2(chunkcoordx + xOffset, chunkcoordy + yOffset);

                if (terrainChunkDictionary.ContainsKey(viewedChunkCoord))
                {
                    terrainChunkDictionary[viewedChunkCoord].UpdateTerrainChunk();
                }
                else
                {
                    terrainChunkDictionary.Add(viewedChunkCoord, new TerrainChunk(viewedChunkCoord, chunkSize, LOD, transform, mapMaterial));
                }

            }
        }
    }

    public class TerrainChunk
    {

        GameObject meshObject;
        Vector2 position;
        Bounds bounds;

        MeshRenderer meshRenderer;
        MeshFilter meshFilter;
        MeshCollider meshCollider;

        LODInfo[] detailLevels;
        LODMesh[] lodMeshes;
        LODMesh collisionLODMesh;

        MapData mapData;
        bool mapDataReceived;
        Vector3 positionV3;
        int previousLODIndex = -1;

        float caveSeed;
        CaveMapGenerator cavemap;
        CaveMeshGenerator cavemesh;


        public TerrainChunk(Vector2 coord, int size, LODInfo[] detailLevels, Transform parent, Material material)
        {
            this.detailLevels = detailLevels;

            position = coord * size;
            bounds = new Bounds(position, Vector2.one * size);
            positionV3 = new Vector3(position.x, 0, position.y);

            meshObject = new GameObject("Chunk");
            meshRenderer = meshObject.AddComponent<MeshRenderer>();
            meshFilter = meshObject.AddComponent<MeshFilter>();
            meshCollider = meshObject.AddComponent<MeshCollider>();
            meshRenderer.material = material;

            meshObject.transform.position = positionV3 * scale;
            meshObject.transform.parent = parent;
            meshObject.transform.localScale = Vector3.one * scale;
            SetVisible(false);


            lodMeshes = new LODMesh[detailLevels.Length];
            for (int i = 0; i < detailLevels.Length; i++)
            {
                lodMeshes[i] = new LODMesh(detailLevels[i].lod, UpdateTerrainChunk);
                if (detailLevels[i].useForCollider)
                {
                    collisionLODMesh = lodMeshes[i];
                }
            }

            mapGenerator.RequestMapData(position, OnMapDataReceived);
        }

        void OnMapDataReceived(MapData mapData)
        {
            this.mapData = mapData;
            mapDataReceived = true;



            Texture2D texture = TextureGenerator.TextureFromColourMap(mapData.colourMap, MapGenerator.ChunkSize, MapGenerator.ChunkSize);
            meshRenderer.material.mainTexture = texture;

            int width = mapData.heightMap.GetLength(0);
            int height = mapData.heightMap.GetLength(1);

            float seedgenerator = 0;
            
            for(int y= 0; y< width; y++)
            {
                for(int x=0; x< width; x++)
                {
                    seedgenerator += mapData.heightMap[x,y];
                    
                }
            }

            caveSeed = (int)Mathf.Abs(seedgenerator - 39000f);
            Instantiate(Resources.Load("CaveEntrance"), new Vector3(positionV3.x, 3, positionV3.z), new Quaternion(0, 0, 0, 0), meshObject.transform);
            GameObject cave = (GameObject)Instantiate(Resources.Load("Cave"), new Vector3(positionV3.x, -100, positionV3.z), new Quaternion(0, 0, 0, 0), meshObject.transform);
            cave.GetComponent<CaveMapGenerator>().Createcave();
            UpdateTerrainChunk();
        }



        public void UpdateTerrainChunk()
        {
            if (mapDataReceived)
            {
                float viewerdstfromedge = Mathf.Sqrt(bounds.SqrDistance(viewerPosition));
                bool visible = viewerdstfromedge <= maxFOV;

                if (visible)
                {
                    int lodIndex = 0;

                    for (int i = 0; i < detailLevels.Length - 1; i++)
                    {
                        if (viewerdstfromedge > detailLevels[i].visibleDistance)
                        {
                            lodIndex = i + 1;
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (lodIndex != previousLODIndex)
                    {
                        LODMesh lodMesh = lodMeshes[lodIndex];
                        if (lodMesh.hasMesh)
                        {
                            previousLODIndex = lodIndex;
                            meshFilter.mesh = lodMesh.mesh;
                        }
                        else if (!lodMesh.meshRequested)
                        {
                            lodMesh.RequestMesh(mapData);
                        }
                    }

                    if (lodIndex == 0)
                    {
                        if (collisionLODMesh.hasMesh)
                        {
                            meshCollider.sharedMesh = collisionLODMesh.mesh;
                        }
                        else if (!collisionLODMesh.meshRequested)
                        {
                            collisionLODMesh.RequestMesh(mapData);
                        }
                    }

                    chunksLastUpdate.Add(this);
                }

                SetVisible(visible);
            }
        }

        public void SetVisible(bool visible)
        {
            meshObject.SetActive(visible);
        }

        public bool IsVisible()
        {
            return meshObject.activeSelf;
        }

    }

    class LODMesh
    {

        public Mesh mesh;
        public bool meshRequested;
        public bool hasMesh;
        int lod;
        System.Action updateCallback;

        public LODMesh(int lod, System.Action updateCallback)
        {
            this.lod = lod;
            this.updateCallback = updateCallback;
        }

        void OnMeshDataReceived(MeshData meshData)
        {
            mesh = meshData.CreateMesh();
            hasMesh = true;

            updateCallback();
        }

        public void RequestMesh(MapData mapData)
        {
            meshRequested = true;
            mapGenerator.RequestMeshData(mapData, lod, OnMeshDataReceived);
        }

    }

    [System.Serializable]
    public struct LODInfo
    {
        public int lod;
        public float visibleDistance;
        public bool useForCollider;
    }

}