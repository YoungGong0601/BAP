using System.Collections;
using UnityEngine;

public static class BulletLineDrawer
{
    class Runner : MonoBehaviour { }

    public static MonoBehaviour coroutineRunner;
    static GameObject bulletLinePrefab;
    // ── Init ──────────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        bulletLinePrefab = Resources.Load<GameObject>("BulletLine");
        GameObject runnerObject = new GameObject("BulletLineDrawerRunner");
        Object.DontDestroyOnLoad(runnerObject);
        coroutineRunner = runnerObject.AddComponent<Runner>();
    }

    public static void DrawLine(Vector3 start, Vector3 end, Color? color = null, float duration = 0.1f)
    {
        GameObject bulletLine = Object.Instantiate(bulletLinePrefab, coroutineRunner.transform);
        LineRenderer lineRenderer = bulletLine.GetComponent<LineRenderer>();

        if (color != null)
        {
            lineRenderer.startColor = color.Value;
            lineRenderer.endColor = color.Value;
        }
        coroutineRunner.StartCoroutine(UpdateLinePoint(lineRenderer, start, end, duration));
    }

    public static IEnumerator UpdateLinePoint(LineRenderer line, Vector3 start, Vector3 end, float duration)
    {
        line.SetPosition(0, start);
        line.SetPosition(1, start);

        var elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            line.SetPosition(1, Vector3.Lerp(start, end, elapsedTime / duration));
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        Object.Destroy(line.gameObject);
    }
}