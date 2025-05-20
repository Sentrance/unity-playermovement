using System.Collections.Generic;
using UnityEngine;

public class CustomGravity
{
    private static List<GravitySource> _gravitySources = new List<GravitySource>();
    
    public static void Register (GravitySource source) {
        Debug.Assert(
            !_gravitySources.Contains(source),
            "[CustomGravity] Duplicate registration of gravity source!", source
        );
        _gravitySources.Add(source);
    }

    public static void Unregister (GravitySource source) {
        Debug.Assert(
            _gravitySources.Contains(source),
            "[CustomGravity] Unregistering an unknown gravity source!", source
        );
        _gravitySources.Remove(source);
    }

    public static Vector3 GetGravity (Vector3 position) {
        Vector3 gravity = Vector3.zero;
        foreach (var source in _gravitySources)
        {
            gravity += source.GetGravity(position);
        }
        return gravity;
    }

    public static Vector3 GetGravity (Vector3 position, out Vector3 upAxis) {
        Vector3 gravity = Vector3.zero;
        foreach (var source in _gravitySources)
        {
            gravity += source.GetGravity(position);
        }
        upAxis = -gravity.normalized;
        return gravity;
    }

    public static Vector3 GetUpAxis (Vector3 position) {
        Vector3 gravity = Vector3.zero;
        foreach (var source in _gravitySources)
        {
            gravity += source.GetGravity(position);
        }
        return -gravity.normalized;
    }
}