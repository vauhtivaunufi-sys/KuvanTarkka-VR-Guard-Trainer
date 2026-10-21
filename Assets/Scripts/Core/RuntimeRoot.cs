using UnityEngine;

// One parent for things made while playing (labels, speech, rays, panels), so they don't clutter the scene root
public static class RuntimeRoot
{
    const string Name = "[Runtime]";
    static Transform root;

    public static void Adopt(GameObject go)
    {
        // A scene reload destroys the old root, Unity then reports it as null
        if (root == null)
        {
            GameObject found = GameObject.Find(Name);
            root = found != null ? found.transform : new GameObject(Name).transform;
        }
        go.transform.SetParent(root, true);
    }
}
