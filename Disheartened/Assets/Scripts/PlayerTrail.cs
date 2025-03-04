using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerTrail : MonoBehaviour
{
    [Header("Trail Settings")]
    [SerializeField] private GameObject trailNodePrefab;
    [SerializeField] private float nodeSpacing = 1.5f;
    [SerializeField] private float maxNodeLifetime = 10f;

    [Header("Debug Settings")]
    [SerializeField] private bool showGizmos = true;

    public static List<Vector3> trailPositions = new List<Vector3>(); // Shared trail data

    private Vector3 lastNodePosition;

    void Start()
    {
        lastNodePosition = transform.position;
    }

    void Update()
    {
        if (Vector3.Distance(transform.position, lastNodePosition) >= nodeSpacing)
        {
            SpawnTrailNode();
            lastNodePosition = transform.position;
        }
    }

    void SpawnTrailNode()
    {
        GameObject node = Instantiate(trailNodePrefab, transform.position, Quaternion.identity);
        HideNode(node);

        trailPositions.Add(transform.position); // Store position for enemies

        // Remove node from list after a delay
        StartCoroutine(RemoveTrailNode(transform.position, maxNodeLifetime));
    }

    void HideNode(GameObject node)
    {
        MeshRenderer meshRenderer = node.GetComponent<MeshRenderer>();
        if (meshRenderer != null) meshRenderer.enabled = false;

        SpriteRenderer spriteRenderer = node.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null) spriteRenderer.enabled = false;
    }

    IEnumerator RemoveTrailNode(Vector3 position, float delay)
    {
        yield return new WaitForSeconds(delay);
        trailPositions.Remove(position);
    }

    void OnDrawGizmos()
    {
        if (!showGizmos || trailPositions == null) return;

        Gizmos.color = Color.green;
        foreach (var pos in trailPositions)
        {
            Gizmos.DrawSphere(pos, 0.2f);
        }
    }
}