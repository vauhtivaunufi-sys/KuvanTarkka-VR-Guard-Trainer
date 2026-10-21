using UnityEngine;

// Thin world-space lines for pointer rays, lasers and taser wires, all sharing one vertex-colour material
public static class LineVisuals
{
    static Material material;

    public static LineRenderer Create(string name, float width, Color color)
    {
        var go = new GameObject(name);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.startWidth = line.endWidth = width;
        line.startColor = line.endColor = color;
        line.numCapVertices = 2;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sharedMaterial = SharedMaterial();
        return line;
    }

    // Shaders that use vertex colours and ship in every build
    public static Material SharedMaterial()
    {
        if (material != null)
            return material;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("UI/Default");
        material = new Material(shader);
        return material;
    }
}
