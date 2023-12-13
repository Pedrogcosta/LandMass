using UnityEngine;
using System.Collections;

public static class MeshGenerator
{

    public static MeshData CreateTerrain(float[,] heightMap, float heightMultiplier, AnimationCurve _heightCurve, int levelOfDetail)
    {
        AnimationCurve heightCurve = new AnimationCurve(_heightCurve.keys);

        int meshLODincrement = (levelOfDetail == 0) ? 1 : levelOfDetail * 2;

        int borderedSize = heightMap.GetLength(0);
        int meshSize = borderedSize - 2 * meshLODincrement;
        int meshSizesimple = borderedSize - 2;

        float topLeftX = (meshSizesimple - 1) / -2f;
        float topLeftZ = (meshSizesimple - 1) / 2f;


        int verticesPerLine = (meshSize - 1) / meshLODincrement + 1;

        MeshData meshData = new MeshData(verticesPerLine);

        int[,] verticeindexmap = new int[borderedSize, borderedSize];
        int meshverticeindex = 0;
        int borderverticeindex = -1;

        for (int y = 0; y < borderedSize; y += meshLODincrement)
        {
            for (int x = 0; x < borderedSize; x += meshLODincrement)
            {
                bool isBorderVertex = y == 0 || y == borderedSize - 1 || x == 0 || x == borderedSize - 1;

                if (isBorderVertex)
                {
                    verticeindexmap[x, y] = borderverticeindex;
                    borderverticeindex--;
                }
                else
                {
                    verticeindexmap[x, y] = meshverticeindex;
                    meshverticeindex++;
                }
            }
        }

        for (int y = 0; y < borderedSize; y += meshLODincrement)
        {
            for (int x = 0; x < borderedSize; x += meshLODincrement)
            {
                int vertexIndex = verticeindexmap[x, y];
                Vector2 percent = new Vector2((x - meshLODincrement) / (float)meshSize, (y - meshLODincrement) / (float)meshSize);
                float height = heightCurve.Evaluate(heightMap[x, y]) * heightMultiplier;
                Vector3 vertexPosition = new Vector3(topLeftX + percent.x * meshSizesimple, height, topLeftZ - percent.y * meshSizesimple);

                meshData.AddVertex(vertexPosition, percent, vertexIndex);

                if (x < borderedSize - 1 && y < borderedSize - 1)
                {
                    int a = verticeindexmap[x, y];
                    int b = verticeindexmap[x + meshLODincrement, y];
                    int c = verticeindexmap[x, y + meshLODincrement];
                    int d = verticeindexmap[x + meshLODincrement, y + meshLODincrement];
                    meshData.AddTriangle(a, d, c);
                    meshData.AddTriangle(d, a, b);
                }

                vertexIndex++;
            }
        }

        meshData.BakeNormals();

        return meshData;

    }
}

public class MeshData
{
    Vector3[] vertices;
    int[] triangles;
    Vector2[] uvs;
    Vector3[] bakedNormals;

    Vector3[] borderVertices;
    int[] borderTriangles;

    int triangleIndex;
    int borderTriangleIndex;

    public MeshData(int verticesPerLine)
    {
        vertices = new Vector3[verticesPerLine * verticesPerLine];
        uvs = new Vector2[verticesPerLine * verticesPerLine];
        triangles = new int[(verticesPerLine - 1) * (verticesPerLine - 1) * 6];

        borderVertices = new Vector3[verticesPerLine * 4 + 4];
        borderTriangles = new int[24 * verticesPerLine];
    }

    public void AddVertex(Vector3 vertexPosition, Vector2 uv, int vertexIndex)
    {
        if (vertexIndex < 0)
        {
            borderVertices[-vertexIndex - 1] = vertexPosition;
        }
        else
        {
            vertices[vertexIndex] = vertexPosition;
            uvs[vertexIndex] = uv;
        }
    }

    public void AddTriangle(int a, int b, int c)
    {
        if (a < 0 || b < 0 || c < 0)
        {
            borderTriangles[borderTriangleIndex] = a;
            borderTriangles[borderTriangleIndex + 1] = b;
            borderTriangles[borderTriangleIndex + 2] = c;
            borderTriangleIndex += 3;
        }
        else
        {
            triangles[triangleIndex] = a;
            triangles[triangleIndex + 1] = b;
            triangles[triangleIndex + 2] = c;
            triangleIndex += 3;
        }
    }

    Vector3[] CalculateNormals()
    {

        Vector3[] verticenormals = new Vector3[vertices.Length];
        int triangleCount = triangles.Length / 3;
        for (int i = 0; i < triangleCount; i++)
        {
            int normalTriangleIndex = i * 3;
            int verticeindexA = triangles[normalTriangleIndex];
            int verticeindexB = triangles[normalTriangleIndex + 1];
            int verticeindexC = triangles[normalTriangleIndex + 2];

            Vector3 triangleNormal = SurfaceNormalFromIndices(verticeindexA, verticeindexB, verticeindexC);
            verticenormals[verticeindexA] += triangleNormal;
            verticenormals[verticeindexB] += triangleNormal;
            verticenormals[verticeindexC] += triangleNormal;
        }

        int borderTriangleCount = borderTriangles.Length / 3;
        for (int i = 0; i < borderTriangleCount; i++)
        {
            int normalTriangleIndex = i * 3;
            int vertexIndexA = borderTriangles[normalTriangleIndex];
            int vertexIndexB = borderTriangles[normalTriangleIndex + 1];
            int vertexIndexC = borderTriangles[normalTriangleIndex + 2];

            Vector3 triangleNormal = SurfaceNormalFromIndices(vertexIndexA, vertexIndexB, vertexIndexC);
            if (vertexIndexA >= 0)
            {
                verticenormals[vertexIndexA] += triangleNormal;
            }
            if (vertexIndexB >= 0)
            {
                verticenormals[vertexIndexB] += triangleNormal;
            }
            if (vertexIndexC >= 0)
            {
                verticenormals[vertexIndexC] += triangleNormal;
            }
        }


        for (int i = 0; i < verticenormals.Length; i++)
        {
            verticenormals[i].Normalize();
        }

        return verticenormals;

    }

    Vector3 SurfaceNormalFromIndices(int indexA, int indexB, int indexC)
    {
        Vector3 pointA = (indexA < 0) ? borderVertices[-indexA - 1] : vertices[indexA];
        Vector3 pointB = (indexB < 0) ? borderVertices[-indexB - 1] : vertices[indexB];
        Vector3 pointC = (indexC < 0) ? borderVertices[-indexC - 1] : vertices[indexC];

        Vector3 sideAB = pointB - pointA;
        Vector3 sideAC = pointC - pointA;
        return Vector3.Cross(sideAB, sideAC).normalized;
    }

    public void BakeNormals()
    {
        bakedNormals = CalculateNormals();
    }

    public Mesh CreateMesh()
    {
        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.normals = bakedNormals;
        return mesh;
    }

}