using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Graphing;
using UnityEditor.ShaderGraph;
using UnityEngine;

namespace ShaderGraphTechniques.Editor
{
    public static class GraphProbe
    {
        public static void PrintShaderNames()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Shader", new[] { "Assets/ShaderGraphTechniques/Runtime" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".shadergraph", StringComparison.OrdinalIgnoreCase)) continue;
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                Debug.Log($"[GraphProbe] SHADER path={path} name={shader?.name ?? "<null>"}");
            }
        }

        public static void Run()
        {
            ProbeTemplate("Packages/com.unity.shadergraph/GraphTemplates/Cross Pipeline/0_Lit Basic.shadergraph");
            ProbeTemplate("Packages/com.unity.shadergraph/GraphTemplates/Cross Pipeline/Unlit Simple.shadergraph");

            var needles = new[] { "SimpleNoise", "SceneDepthDifference", "SceneColor", "Triplanar", "Fresnel", "NormalVector", "ScreenPosition", "TransformNode" };
            foreach (var type in AppDomain.CurrentDomain.GetAssemblies()
                         .SelectMany(SafeTypes)
                         .Where(type => needles.Any(needle => type.Name.Contains(needle, StringComparison.OrdinalIgnoreCase)))
                         .OrderBy(type => type.FullName))
            {
                Debug.Log($"[GraphProbe] TYPE {type.Assembly.GetName().Name} :: {type.FullName}");
                if (!typeof(AbstractMaterialNode).IsAssignableFrom(type) || type.IsAbstract) continue;
                try
                {
                    var node = (AbstractMaterialNode)Activator.CreateInstance(type);
                    node.UpdateNodeAfterDeserialization();
                    foreach (var slot in node.GetInputSlots<MaterialSlot>())
                        Debug.Log($"[GraphProbe]   IN id={slot.id} name={slot.displayName} type={slot.GetType().Name}");
                    foreach (var slot in node.GetOutputSlots<MaterialSlot>())
                        Debug.Log($"[GraphProbe]   OUT id={slot.id} name={slot.displayName} type={slot.GetType().Name}");
                    foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                        Debug.Log($"[GraphProbe]   FIELD {field.FieldType.Name} {field.Name}={SafeValue(field, node)}");
                    foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                                 .Where(property => property.GetIndexParameters().Length == 0))
                        Debug.Log($"[GraphProbe]   PROP {property.PropertyType.Name} {property.Name}={SafeValue(property, node)}");
                }
                catch (Exception exception)
                {
                    Debug.Log($"[GraphProbe]   INSTANTIATE_FAILED {exception.GetType().Name}: {exception.Message}");
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[GraphProbe] COMPLETE");
        }

        static void ProbeTemplate(string path)
        {
            if (!FileUtilities.TryReadGraphDataFromDisk(path, out var graph) || graph == null)
                throw new InvalidOperationException($"Could not read {path}");
            Debug.Log($"[GraphProbe] TEMPLATE {path}");
            foreach (var target in graph.activeTargets)
                Debug.Log($"[GraphProbe]   TARGET {target.GetType().AssemblyQualifiedName}");
            foreach (var block in graph.GetNodes<BlockNode>())
                Debug.Log($"[GraphProbe]   BLOCK {block.descriptor} :: {block.descriptor.displayName}");
        }

        static Type[] SafeTypes(Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch (ReflectionTypeLoadException exception) { return exception.Types.Where(type => type != null).ToArray(); }
        }

        static string SafeValue(FieldInfo field, object target)
        {
            try { return field.GetValue(target)?.ToString() ?? "null"; }
            catch { return "<unreadable>"; }
        }

        static string SafeValue(PropertyInfo property, object target)
        {
            try { return property.CanRead ? property.GetValue(target)?.ToString() ?? "null" : "<write-only>"; }
            catch { return "<unreadable>"; }
        }
    }
}
