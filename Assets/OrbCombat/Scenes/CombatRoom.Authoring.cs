using UnityEngine;

namespace OrbCombat
{
    public partial class CombatRoom
    {
#if UNITY_EDITOR
        [UnityEditor.MenuItem("Tools/Orb Combat/Build Editable Scene Objects")]
        private static void BuildCurrentScene()
        {
            var room = Object.FindFirstObjectByType<CombatRoom>();
            if (room != null) room.BuildEditableSceneObjects();
        }
#endif
        private System.Numerics.Vector2 RoomMinimum => new System.Numerics.Vector2(
            leftWall == null ? -CombatRound.HalfWidth : leftWall.position.x + leftWall.lossyScale.x * 0.5f,
            bottomWall == null ? -CombatRound.HalfDepth : bottomWall.position.z + bottomWall.lossyScale.z * 0.5f);
        private System.Numerics.Vector2 RoomMaximum => new System.Numerics.Vector2(
            rightWall == null ? CombatRound.HalfWidth : rightWall.position.x - rightWall.lossyScale.x * 0.5f,
            topWall == null ? CombatRound.HalfDepth : topWall.position.z - topWall.lossyScale.z * 0.5f);

        [ContextMenu("Build Editable Scene Objects")]
        private void BuildEditableSceneObjects()
        {
#if UNITY_EDITOR
            if (Application.isPlaying || sceneObjectsBuilt) return;
            UnityEditor.Undo.RegisterFullObjectHierarchyUndo(gameObject, "Build combat scene");
            var existing = new System.Collections.Generic.HashSet<GameObject>(gameObject.scene.GetRootGameObjects());
            Start();
            if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/OrbCombat/Art/Materials"))
                UnityEditor.AssetDatabase.CreateFolder("Assets/OrbCombat/Art", "Materials");
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                if (existing.Contains(root)) continue;
                UnityEditor.Undo.RegisterCreatedObjectUndo(root, "Build combat scene");
                foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                {
                    var properties = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(properties);
                    var material = new Material(renderer.sharedMaterial);
                    material.color = properties.GetColor("_Color");
                    string path = UnityEditor.AssetDatabase.GenerateUniqueAssetPath(
                        "Assets/OrbCombat/Art/Materials/" + renderer.name + ".mat");
                    UnityEditor.AssetDatabase.CreateAsset(material, path);
                    renderer.sharedMaterial = material;
                    renderer.SetPropertyBlock(null);
                }
                if (root.name == "Side wall")
                {
                    if (root.transform.position.x < 0) leftWall = root.transform;
                    else rightWall = root.transform;
                }
                if (root.name == "End wall")
                {
                    if (root.transform.position.z < 0) bottomWall = root.transform;
                    else topWall = root.transform;
                }
            }
            spike.SetParent(spikePosition, true);
            bankMark.SetParent(openingMark, true);
            sceneObjectsBuilt = true;
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
        }
    }
}
