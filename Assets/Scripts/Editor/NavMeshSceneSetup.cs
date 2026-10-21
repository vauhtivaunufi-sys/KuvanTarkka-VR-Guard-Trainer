using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// Editor helpers for NavMesh bookkeeping in the level FBX, safe to re-run, objects already set up are left alone
static class NavMeshSceneSetup
{
    const string ObstacleChildName = "DoorObstacle";

    // Level FBX objects kept out of the bake, otherwise the ceiling bakes as a walkable area above the level
    static readonly string[] ExcludedExactNames = { "Ceiling_1" };
    static readonly string[] ExcludedNamePrefixes = { "Lamp_" };

    [MenuItem("Tools/KuvanTarkka/NavMesh/Set Up Doors")]
    static void SetUpDoors()
    {
        Door[] doors = Object.FindObjectsByType<Door>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (doors.Length == 0)
        {
            Debug.LogWarning("NavMesh setup: no Door components found in the open scene.");
            return;
        }

        int modifiersAdded = 0, obstaclesAdded = 0, alreadyDone = 0, unsized = 0;

        foreach (Door door in doors)
        {
            GameObject go = door.gameObject;
            bool touched = false;

            // Closed leaf stays out of the bake, the obstacle below blocks the doorway at runtime
            NavMeshModifier modifier = go.GetComponent<NavMeshModifier>();
            if (modifier == null)
            {
                modifier = Undo.AddComponent<NavMeshModifier>(go);
                modifiersAdded++;
                touched = true;
            }
            if (!modifier.ignoreFromBuild || !modifier.applyToChildren)
            {
                Undo.RecordObject(modifier, "Set Up Door NavMesh");
                modifier.ignoreFromBuild = true;
                modifier.applyToChildren = true;
                EditorUtility.SetDirty(modifier);
                touched = true;
            }

            if (FindObstacle(door.transform) == null)
            {
                if (CreateObstacle(door)) obstaclesAdded++;
                else unsized++;
                touched = true;
            }

            if (!touched) alreadyDone++;
        }

        if (modifiersAdded > 0 || obstaclesAdded > 0)
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log($"NavMesh setup: {doors.Length} doors - added {modifiersAdded} modifier(s), {obstaclesAdded} obstacle(s), " +
                  $"{alreadyDone} already set up, {unsized} skipped (no renderer to size an obstacle from). " +
                  "Re-bake the NavMeshSurface for this to take effect.");
    }

    [MenuItem("Tools/KuvanTarkka/NavMesh/Exclude Ceiling And Lamps")]
    static void ExcludeCeilingAndLamps()
    {
        List<Transform> targets = FindExcludedObjects();
        if (targets.Count == 0)
        {
            Debug.LogWarning("NavMesh setup: found no ceiling or lamp objects to exclude - check the names in the level FBX.");
            return;
        }

        int added = 0;
        foreach (Transform t in targets)
        {
            NavMeshModifier modifier = t.GetComponent<NavMeshModifier>();
            if (modifier == null)
            {
                modifier = Undo.AddComponent<NavMeshModifier>(t.gameObject);
                added++;
            }
            if (!modifier.ignoreFromBuild || !modifier.applyToChildren)
            {
                Undo.RecordObject(modifier, "Exclude From NavMesh Bake");
                modifier.ignoreFromBuild = true;
                modifier.applyToChildren = true;
                EditorUtility.SetDirty(modifier);
            }
        }

        if (added > 0)
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log($"NavMesh setup: excluded {targets.Count} ceiling/lamp object(s) from the bake ({added} new modifier(s)). " +
                  "Re-bake the NavMeshSurface for this to take effect.");
    }

    // Reports what is missing without changing anything
    [MenuItem("Tools/KuvanTarkka/NavMesh/Report Setup State")]
    static void ReportState()
    {
        Door[] doors = Object.FindObjectsByType<Door>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Door[] missing = doors.Where(d => !HasIgnoreModifier(d.gameObject) || FindObstacle(d.transform) == null).ToArray();

        List<Transform> excluded = FindExcludedObjects();
        int excludedDone = excluded.Count(t => HasIgnoreModifier(t.gameObject));

        Debug.Log($"NavMesh state: doors {doors.Length - missing.Length}/{doors.Length} set up, " +
                  $"ceiling+lamps {excludedDone}/{excluded.Count} excluded.");

        foreach (Door door in missing)
        {
            string what = !HasIgnoreModifier(door.gameObject) ? "no Ignore From Build modifier" : "no DoorObstacle";
            Debug.Log($"  incomplete: {GetPath(door.transform)} - {what}", door);
        }
    }

    static List<Transform> FindExcludedObjects()
    {
        var targets = new List<Transform>();
        foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            targets.AddRange(root.GetComponentsInChildren<Transform>(true).Where(t => IsExcluded(t.name)));
        return targets;
    }

    static bool HasIgnoreModifier(GameObject go)
    {
        NavMeshModifier modifier = go.GetComponent<NavMeshModifier>();
        return modifier != null && modifier.ignoreFromBuild && modifier.applyToChildren;
    }

    static bool IsExcluded(string name)
    {
        return ExcludedExactNames.Contains(name) || ExcludedNamePrefixes.Any(name.StartsWith);
    }

    static NavMeshObstacle FindObstacle(Transform door)
    {
        foreach (Transform child in door)
        {
            NavMeshObstacle obstacle = child.GetComponent<NavMeshObstacle>();
            if (obstacle != null) return obstacle;
        }
        return null;
    }

    // Obstacle on the leaf swings with it, closed it carves the doorway shut
    static bool CreateObstacle(Door door)
    {
        if (!TryGetLocalBounds(door.transform, out Bounds local))
        {
            Debug.LogWarning($"NavMesh setup: '{GetPath(door.transform)}' has no mesh, so no obstacle was created for it.", door);
            return false;
        }

        var go = new GameObject(ObstacleChildName);
        Undo.RegisterCreatedObjectUndo(go, "Set Up Door NavMesh");
        go.transform.SetParent(door.transform, false);
        go.transform.localPosition = local.center;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        NavMeshObstacle obstacle = Undo.AddComponent<NavMeshObstacle>(go);
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.center = Vector3.zero;
        obstacle.size = local.size;
        obstacle.carving = true;
        obstacle.carveOnlyStationary = true;
        return true;
    }

    // Mesh bounds in the door's own space, world bounds are far too big for a door at an angle
    static bool TryGetLocalBounds(Transform door, out Bounds bounds)
    {
        bounds = default;
        bool any = false;

        foreach (MeshFilter filter in door.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null)
                continue;

            Matrix4x4 toDoor = door.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            Bounds mesh = filter.sharedMesh.bounds;

            for (int corner = 0; corner < 8; corner++)
            {
                var sign = new Vector3(
                    (corner & 1) == 0 ? -1f : 1f,
                    (corner & 2) == 0 ? -1f : 1f,
                    (corner & 4) == 0 ? -1f : 1f);
                Vector3 point = toDoor.MultiplyPoint3x4(mesh.center + Vector3.Scale(mesh.extents, sign));

                if (any)
                {
                    bounds.Encapsulate(point);
                }
                else
                {
                    bounds = new Bounds(point, Vector3.zero);
                    any = true;
                }
            }
        }

        return any;
    }

    static string GetPath(Transform t)
    {
        string path = t.name;
        for (Transform p = t.parent; p != null; p = p.parent)
            path = p.name + "/" + path;
        return path;
    }
}
