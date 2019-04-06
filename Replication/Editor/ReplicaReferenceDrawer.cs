using UnityEditor;
using UnityEngine;

namespace Cube.Replication {
    [CustomPropertyDrawer(typeof(ReplicaReference))]
    public class ReplicaReferenceDrawer : PropertyDrawer {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            var replica = property.FindPropertyRelative("_replica");

            EditorGUI.BeginChangeCheck();
            var value = (Replica)EditorGUI.ObjectField(position, label, replica.objectReferenceValue, typeof(Replica), true);
            if (EditorGUI.EndChangeCheck()) {
                replica.objectReferenceValue = value;
            }
        }
    }
}