using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    public static class EComponentContextMenu
    {
        private static List<ComponentData> _copiedComponents = new List<ComponentData>();
        private static bool _lastCopyWasSingleComponent = false;
        private class ComponentData
        {
            public Type ComponentType;
            public Dictionary<string, object> Data = new Dictionary<string, object>();
        }

        [MenuItem("CONTEXT/Component/Copy All and Transforms", false, 1510)]
        private static void CopyAllAndTransforms(MenuCommand command)
        {
            _lastCopyWasSingleComponent = false;
            CopyComponentsWithSelection(true);
        }

        [MenuItem("CONTEXT/Component/Copy All", false, 1511)]
        private static void CopyAll(MenuCommand command)
        {
            _lastCopyWasSingleComponent = false;
            CopyComponentsWithSelection(false);
        }
    
        [MenuItem("CONTEXT/Component/Copy Component to Buffer", false, 1512)]
        private static void CopyComponentToBuffer(MenuCommand command)
        {
            Component component = command.context as Component;
            if (component == null)
            {
                LogError("No component selected.");
                return;
            }

            if (!_lastCopyWasSingleComponent)
            {
                _copiedComponents.Clear();
            }

            _copiedComponents.Add(CopyComponentData(component));
            _lastCopyWasSingleComponent = true; 
            LogMessage("Component data copied to buffer.");
        }

        [MenuItem("CONTEXT/Component/Paste All", false, 1513)]
        private static void PasteAll(MenuCommand command)
        {
            GameObject target = Selection.activeGameObject;
            if (target == null)
                LogError("No target GameObject for pasting components."); 
            else
                PasteComponents(target);
        }
 
        [MenuItem("CONTEXT/Component/Paste All and Update Values", false, 1514)]

        private static void PasteAllAndUpdateValues(MenuCommand command)
        {
            GameObject target = Selection.activeGameObject;
            if (target == null)
                LogError("No target GameObject for pasting and updating components.");
            else
                PasteComponentsAndUpdateValues(target);
        }
    
        private static void CopyComponentsWithSelection(bool includeTransform)
        {
            GameObject go = Selection.activeGameObject;
            if (go == null)
                LogError("No GameObject selected.");
            else
            {
                CopyComponents(go, includeTransform);
                LogMessage(includeTransform ? "Copied all components and Transforms." : "Copied all components.");
            }
        }

        private static void CopyComponents(GameObject go, bool includeTransform)
        {
            _copiedComponents = go.GetComponents<Component>()
                .Where(c => includeTransform || !(c is Transform))
                .Select(CopyComponentData)
                .ToList();
        }

        private static ComponentData CopyComponentData(Component component)
        {
            var data = new ComponentData { ComponentType = component.GetType() };
            BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic;
            data.Data = component.GetType()
                .GetFields(bindingFlags)
                .Where(f => f.IsPublic || Attribute.IsDefined(f, typeof(SerializeField)))
                .ToDictionary(f => f.Name, f => f.GetValue(component));

            if (component is Transform transform)
            {
                data.Data["position"] = transform.localPosition;
                data.Data["rotation"] = transform.localRotation;
                data.Data["scale"] = transform.localScale;
            }

            return data;
        }

        private static void PasteComponents(GameObject target)
        {
            Undo.RegisterCompleteObjectUndo(target, "Paste All Components");
            _copiedComponents.ForEach(data => PasteComponentData(target, data));
            LogMessage("Pasted all components.");
        }

        private static void PasteComponentData(GameObject target, ComponentData data)
        {
            if (data.ComponentType == typeof(Transform))
                PasteTransformData(target.transform, data);
            else
                PasteOtherComponentData(target, data);
        }

        private static void PasteTransformData(Transform transform, ComponentData data)
        {
            Undo.RecordObject(transform, "Change Transform");
            transform.localPosition = (Vector3)data.Data["position"];
            transform.localRotation = (Quaternion)data.Data["rotation"];
            transform.localScale = (Vector3)data.Data["scale"];
        }

        private static void PasteOtherComponentData(GameObject target, ComponentData data)
        {
            Component newComponent = target.AddComponent(data.ComponentType);
            Undo.RegisterCreatedObjectUndo(newComponent, "Create Component");
            BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic;
            data.Data.ToList().ForEach(pair =>
            {
                FieldInfo field = data.ComponentType.GetField(pair.Key, bindingFlags);
                if (field != null && (field.IsPublic || Attribute.IsDefined(field, typeof(SerializeField))))
                {
                    Undo.RecordObject(newComponent, "Change Field");
                    field.SetValue(newComponent, pair.Value);
                }
            });
        }
    
        private static void PasteComponentsAndUpdateValues(GameObject target)
        {
            Undo.RegisterCompleteObjectUndo(target, "Paste and Update All Components");
            foreach (var data in _copiedComponents)
            {
                Component existingComponent = target.GetComponent(data.ComponentType);
                if (existingComponent != null)
                    UpdateComponentData(existingComponent, data);
                else
                    PasteComponentData(target, data);
            }
            LogMessage("Pasted and updated all components.");
        }

        private static void UpdateComponentData(Component component, ComponentData data)
        {
            Undo.RecordObject(component, "Update Component");
            if (component is Transform transform)
                UpdateTransformData(transform, data);
            else
                UpdateNonTransformComponentData(component, data);
        }

        private static void UpdateTransformData(Transform transform, ComponentData data)
        {
            transform.localPosition = (Vector3)data.Data["position"];
            transform.localRotation = (Quaternion)data.Data["rotation"];
            transform.localScale = (Vector3)data.Data["scale"];
        }

        private static void UpdateNonTransformComponentData(Component component, ComponentData data)
        {
            BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic;
            foreach (var pair in data.Data)
            {
                FieldInfo field = data.ComponentType.GetField(pair.Key, bindingFlags);
                if (field != null && (field.IsPublic || Attribute.IsDefined(field, typeof(SerializeField))))
                    field.SetValue(component, pair.Value);
            }
        }
     
        private static void LogError(string message) => Debug.LogError(message);
        private static void LogMessage(string message) => Debug.Log(message);
    }
}
