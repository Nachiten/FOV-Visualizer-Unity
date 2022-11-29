using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class FieldOfView : MonoBehaviour
{
    public float viewRadius;
    [Range(0, 360)] public float viewAngle;

    public LayerMask targetMask;
    public LayerMask obstacleMask;

    [HideInInspector] public List<Transform> visibleTargets = new();
    private List<Transform> allTargets;

    public float meshResolution;
    public int edgeResolveIterations;
    public float edgeDstThreshold;

    public float maskCutawayDst = .1f;

    public MeshFilter viewMeshFilter;
    private Mesh viewMesh;

    private void Awake()
    {
        // All targets is all the gameobjects in layer "Targets"
        GameObject[] possibleTargets = GameObject.FindGameObjectsWithTag("Target");
        allTargets = new List<Transform>();
        
        foreach (GameObject target in possibleTargets)
        {
            allTargets.Add(target.transform);
        }
        
        viewMesh = new Mesh { name = "View Mesh" };
        viewMeshFilter.mesh = viewMesh;
    }

    private void Start()
    {
        StartCoroutine(nameof(FindTargetsWithDelay), .2f);
    }

    private IEnumerator FindTargetsWithDelay(float delay)
    {
        while (true)
        {
            yield return new WaitForSeconds(delay);
            FindVisibleTargets();
            Cosa();
        }
    }

    // private void Update()
    // {
    //     // ShowAndHideTargets();
    // }

    private void Cosa()
    {
        // Hide mesh renderer of every target
        foreach (Transform target in allTargets)
        {
            target.GetComponent<MeshRenderer>().enabled = visibleTargets.Contains(target);
        }
    }
    
    private void LateUpdate()
    {
        DrawFieldOfView();
    }

    // private void ShowAndHideTargets()
    // {
    //     Hide or show object depending on visible
    //     foreach (Transform target in allTargets)
    //     {
    //         target.gameObject.SetActive(visibleTargets.Contains(target));
    //     }
    // }

    private void FindVisibleTargets()
    {
        visibleTargets.Clear();
        Collider[] targetsInViewRadius = Physics.OverlapSphere(transform.position, viewRadius, targetMask);

        foreach (Collider theCollider in targetsInViewRadius)
        {
            Transform target = theCollider.transform;
            Vector3 dirToTarget = (target.position - transform.position).normalized;
            
            // Me no entender
            if (!(Vector3.Angle(transform.forward, dirToTarget) < viewAngle / 2)) 
                continue;
            
            float distanceToTarget = Vector3.Distance(transform.position, target.position);
            
            // There is an obstacle
            if (Physics.Raycast(transform.position, dirToTarget, distanceToTarget, obstacleMask))
                continue;
                
            visibleTargets.Add(target);
        }
    }

    private void DrawFieldOfView()
    {
        int stepCount = Mathf.RoundToInt(viewAngle * meshResolution);
        float stepAngleSize = viewAngle / stepCount;
        List<Vector3> viewPoints = new();
        ViewCastInfo oldViewCast = new();
        
        for (int i = 0; i <= stepCount; i++)
        {
            float angle = transform.eulerAngles.y - viewAngle / 2 + stepAngleSize * i;
            ViewCastInfo newViewCast = ViewCast(angle);

            if (i > 0)
            {
                bool edgeDstThresholdExceeded = Mathf.Abs(oldViewCast.dst - newViewCast.dst) > edgeDstThreshold;
                if (oldViewCast.hit != newViewCast.hit || (oldViewCast.hit && edgeDstThresholdExceeded))
                {
                    EdgeInfo edge = FindEdge(oldViewCast, newViewCast);
                    if (edge.pointA != Vector3.zero)
                    {
                        viewPoints.Add(edge.pointA);
                    }

                    if (edge.pointB != Vector3.zero)
                    {
                        viewPoints.Add(edge.pointB);
                    }
                }
            }

            viewPoints.Add(newViewCast.point);
            oldViewCast = newViewCast;
        }

        int vertexCount = viewPoints.Count + 1;
        Vector3[] vertices = new Vector3[vertexCount];
        int[] triangles = new int[(vertexCount - 2) * 3];

        vertices[0] = Vector3.zero;
        for (int i = 0; i < vertexCount - 1; i++)
        {
            vertices[i + 1] = transform.InverseTransformPoint(viewPoints[i]) + Vector3.forward * maskCutawayDst;

            if (i >= vertexCount - 2) 
                continue;
            
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = i + 2;
        }

        viewMesh.Clear();

        viewMesh.vertices = vertices;
        viewMesh.triangles = triangles;
        viewMesh.RecalculateNormals();
    }

    private EdgeInfo FindEdge(ViewCastInfo minViewCast, ViewCastInfo maxViewCast)
    {
        float minAngle = minViewCast.angle;
        float maxAngle = maxViewCast.angle;
        Vector3 minPoint = Vector3.zero;
        Vector3 maxPoint = Vector3.zero;

        for (int i = 0; i < edgeResolveIterations; i++)
        {
            float angle = (minAngle + maxAngle) / 2;
            ViewCastInfo newViewCast = ViewCast(angle);

            bool edgeDstThresholdExceeded = Mathf.Abs(minViewCast.dst - newViewCast.dst) > edgeDstThreshold;
            if (newViewCast.hit == minViewCast.hit && !edgeDstThresholdExceeded)
            {
                minAngle = angle;
                minPoint = newViewCast.point;
            }
            else
            {
                maxAngle = angle;
                maxPoint = newViewCast.point;
            }
        }

        return new EdgeInfo(minPoint, maxPoint);
    }

    private ViewCastInfo ViewCast(float globalAngle)
    {
        Vector3 dir = DirFromAngle(globalAngle, true);

        return Physics.Raycast(transform.position, dir, out RaycastHit hit, viewRadius, obstacleMask) ? 
            new ViewCastInfo(true, hit.point, hit.distance, globalAngle) : 
            new ViewCastInfo(false, transform.position + dir * viewRadius, viewRadius, globalAngle);
    }

    public Vector3 DirFromAngle(float angleInDegrees, bool angleIsGlobal)
    {
        if (!angleIsGlobal)
            angleInDegrees += transform.eulerAngles.y;
        
        return new Vector3(Mathf.Sin(angleInDegrees * Mathf.Deg2Rad), 0, Mathf.Cos(angleInDegrees * Mathf.Deg2Rad));
    }

    private struct ViewCastInfo
    {
        public readonly bool hit;
        public readonly Vector3 point;
        public readonly float dst;
        public readonly float angle;

        public ViewCastInfo(bool _hit, Vector3 _point, float _dst, float _angle)
        {
            hit = _hit;
            point = _point;
            dst = _dst;
            angle = _angle;
        }
    }

    private struct EdgeInfo
    {
        public readonly Vector3 pointA;
        public readonly Vector3 pointB;

        public EdgeInfo(Vector3 _pointA, Vector3 _pointB)
        {
            pointA = _pointA;
            pointB = _pointB;
        }
    }
}