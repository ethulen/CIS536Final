using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

public class RayTracer : MonoBehaviour
{
    public RenderTexture renderTexture;
    public int width = 512;
    public int height = 512;
    public int maxDepth = 5;
    public int sampleCount = 1;
    public float fov = 60f;
    public Color backgroundColor = Color.black;

    private List<IRayTracingObject> objects = new List<IRayTracingObject>();
    private Camera cam;
    private NativeArray<Color32> pixels;

    private struct RayTracingJob : IJobParallelFor
    {
        public int width;
        public int height;
        public float fov;
        public int maxDepth;
        public int sampleCount;
        public Color backgroundColor;
        public NativeArray<Color32> pixels;
        public RayTracingObject[] objects;

        public void Execute(int i)
        {
            int x = i % width;
            int y = i / width;

            Vector3 rayOrigin = Vector3.zero;
            Vector3 rayDirection = GetRayDirection(x, y, width, height, fov, cam.transform);
            Color color = TraceRay(rayOrigin, rayDirection, maxDepth);

            for (int j = 0; j < sampleCount; j++)
            {
                color += TraceRay(rayOrigin, SampleRay(rayDirection), maxDepth);
            }

            color /= (float) (sampleCount + 1);

            pixels[i] = color;
        }

        private Vector3 GetRayDirection(int x, int y, int width, int height, float fov, Transform camTransform)
        {
            float aspectRatio = (float) width / (float) height;
            float tanFov = Mathf.Tan(fov * Mathf.Deg2Rad * 0.5f);

            float screenX = (2f * ((float) x + 0.5f) / (float) width - 1f) * tanFov * aspectRatio;
            float screenY = (1f - 2f * ((float) y + 0.5f) / (float) height) * tanFov;

            Vector3 direction = new Vector3(screenX, screenY, -1f);
            direction = camTransform.TransformDirection(direction);
            direction.Normalize();

            return direction;
        }

        private Color TraceRay(Vector3 origin, Vector3 direction, int depth)
        {
            if (depth <= 0)
            {
                return backgroundColor;
            }

            RayTracingObject hitObject = null;
            float closestHit = Mathf.Infinity;

            foreach (RayTracingObject obj in objects)
            {
                float hitDistance = obj.Intersect(origin, direction);

                if (hitDistance >= 0f && hitDistance < closestHit)
                {
                    closestHit = hitDistance;
                    hitObject = obj;
                }
            }

            if (hitObject != null)
            {
                Vector3 hitPoint = origin + direction * closestHit;
                Vector3 normal = hitObject.GetNormal(hitPoint);
                Vector3 reflection = Vector3.Reflect(direction, normal);
                Color surfaceColor = hitObject.GetColor(hitPoint);

                Color reflectionColor = TraceRay(hitPoint + normal * 0.001f, reflection, depth - 1);
                Color finalColor = surfaceColor * reflectionColor;

                return finalColor;
            }
            else
            {
                return backgroundColor;
            }
        }

            private Vector3 SampleRay(Vector3 direction)
    {
        float phi = Random.Range(0f, Mathf.PI * 2f);
        float cosTheta = Random.Range(0f, 1f);
        float sinTheta = Mathf.Sqrt(1f - cosTheta * cosTheta);

        Vector3 u = Vector3.Cross(direction, Vector3.up).normalized;
        Vector3 v = Vector3.Cross(direction, u).normalized;
        Vector3 sampleDirection = direction + (u * Mathf.Cos(phi) * sinTheta + v * Mathf.Sin(phi) * sinTheta + direction * cosTheta);

        return sampleDirection.normalized;
    }
}

private void Awake()
{
    cam = GetComponent<Camera>();
    cam.targetTexture = renderTexture;
    pixels = new NativeArray<Color32>(width * height, Allocator.Persistent);
}

private void OnDisable()
{
    pixels.Dispose();
}

private void OnRenderImage(RenderTexture source, RenderTexture destination)
{
    RayTracingJob job = new RayTracingJob
    {
        width = width,
        height = height,
        fov = fov,
        maxDepth = maxDepth,
        sampleCount = sampleCount,
        backgroundColor = backgroundColor,
        pixels = pixels,
        objects = objects.ToArray()
    };

    JobHandle jobHandle = job.Schedule(width * height, 64);
    jobHandle.Complete();

    Texture2D texture = new Texture2D(width, height);
    Color32[] pixelArray = pixels.ToArray();
    texture.SetPixels32(pixelArray);
    texture.Apply();

    Graphics.Blit(texture, destination);

    Destroy(texture);
}

public void AddObject(IRayTracingObject obj)
{
    objects.Add(obj as RayTracingObject);
}

public void RemoveObject(IRayTracingObject obj)
{
    objects.Remove(obj as RayTracingObject);
}
}