using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShowRangeTest : MonoBehaviour
{
    public Vector3 Range;
    private void OnDrawGizmos() {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(Range.x, 0.1f, Range.z));
    }
}
