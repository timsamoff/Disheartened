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

    public static PlayerTrail instance; // Singleton access

    private List<GameObject> trailNodes = new List<GameObject>(); // Store node objects
    public List<Vector3> trailPositions = new List<Vector3>(); // Public for enemies

    private Vector3 lastNodePosition;

    void Awake()
    {
        if (instance == null)
            instance = this;
        else
            Destroy(gameObject);
    }

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

        Debug.Log("Trail size: " + trailPositions.Count);
    }

    void SpawnTrailNode()
    {
        GameObject node = Instantiate(trailNodePrefab, transform.position, Quaternion.identity);
        HideNode(node);

        trailNodes.Add(node);
        trailPositions.Add(node.transform.position); // Track position for enemies

        // Remove node from list after a delay
        StartCoroutine(RemoveTrailNode(node, maxNodeLifetime));
    }

    void HideNode(GameObject node)
    {
        if (node.TryGetComponent(out MeshRenderer meshRenderer))
            meshRenderer.enabled = false;

        if (node.TryGetComponent(out SpriteRenderer spriteRenderer))
            spriteRenderer.enabled = false;
    }

    IEnumerator RemoveTrailNode(GameObject node, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (trailNodes.Contains(node))
        {
            trailPositions.Remove(node.transform.position);
            trailNodes.Remove(node);
            Destroy(node);
        }
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

    // **Added method for retrieving trail nodes**
    public List<Vector3> GetTrailNodes()
    {
        return new List<Vector3>(trailPositions);
    }
}