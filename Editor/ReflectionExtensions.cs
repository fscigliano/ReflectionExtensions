using UnityEditor; 
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace com.fscigliano.ReflectionExtensions.Editor
{
    /// <summary>
    /// Creation Date:   4/5/2020 10:30:32 PM
    /// Product Name:    Reflection Extensions
    /// Developers:      Franco Scigliano
    /// Description:     From : https://www.tangledrealitystudios.com/code-examples/flexible-editor-property-fields-unity/
    ///                  Researched from:
    ///                  https://answers.unity.com/questions/929293/get-field-type-of-serializedproperty.html
    ///                  https://stackoverflow.com/questions/7072088/why-does-type-getelementtype-return-null
    /// </summary>
    public static class ReflectionExtensions
    {
        private static Dictionary<object, Dictionary<string, object>> _targetObjectOfPropertyCache = new ();

        public static Type GetType(SerializedProperty property)
        {
            string[] splitPropertyPath = property.propertyPath.Split('.');
            Type type = property.serializedObject.targetObject.GetType();
 
            for (int i = 0; i < splitPropertyPath.Length; i++)
            {
                if (splitPropertyPath[i] == "Array")
                {
                    type = type.GetEnumerableType();
                    i++; //skip "data[x]"
                }
                else
                    type = type.GetField(splitPropertyPath[i], BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy | BindingFlags.Instance).FieldType;
            }
 
            return type;
        }
        public static Assembly GetAssemblyFor(string propType)
        {
            var all = System.AppDomain.CurrentDomain.GetAssemblies();
            foreach (var a in all)
            {
                var e = a.GetType(propType);
                if (e != null)
                    return a;
                var allTypes = a.GetTypes();
                foreach (var t in allTypes)
                {
                    if (t.Name == propType)
                    {
                        return a;
                    }
                }
                
            }
            return null;
        }
        public static Type GetEnumerableType(this Type type)
        {
            if (type == null)
                throw new ArgumentNullException("type");
 
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                return type.GetGenericArguments()[0];
 
            var iface = (from i in type.GetInterfaces()
                where i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>)
                select i).FirstOrDefault();
 
            if (iface == null)
                throw new ArgumentException("Does not represent an enumerable type.", "type");
 
            return GetEnumerableType(iface);
        }
        public static PropertyInfo GetProperty(string p, object o)
        {
            var oType = o.GetType();
            var result = oType.GetProperty(p, BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance);
            return result;
        }
        public static PropertyInfo GetProtectedProperty(string p, object o)
        {
            var result = o.GetType().GetProperty(p, BindingFlags.NonPublic|BindingFlags.Instance);
            return result;
        }
        public static PropertyInfo GetPublicProperty(string p, object o)
        {
            var result = o.GetType().GetProperty(p, BindingFlags.Public|BindingFlags.Instance);
            return result;
        }
        public static string GetFromProp(PropertyInfo pInfo, object o)
        {
            string v = pInfo.GetValue(o, null) as string;
            return v;
        }
        public static Type GetPropertyType(SerializedProperty prop)
        {
            //gets parent type info
            string[] slices = prop.propertyPath.Split('.');
            System.Type type = prop.serializedObject.targetObject.GetType();

            for (int i = 0; i < slices.Length; i++)
            {
                if (slices[i] == "Array")
                {
                    i++; //skips "data[x]"
                    type = type.GetElementType(); //gets info on array elements
                }

                //gets info on field and its type
                else
                {
                    type = type.GetField(slices[i],
                        BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy |
                        BindingFlags.Instance).FieldType;
                }
            }
            return type;
        }
        private static object GetValue_Imp(object source, string name)
        {
            if (source == null)
                return null;
            var type = source.GetType();
            while (type != null)
            {
                var f = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                if (f != null)
                    return f.GetValue(source);
                var p = type.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (p != null)
                    return p.GetValue(source, null);
                type = type.BaseType;
            }
            return null;
        }
        private static object GetValue_Imp(object source, string name, int index)
        {
            var enumerable = GetValue_Imp(source, name) as System.Collections.IEnumerable;
            if (enumerable == null) return null;
            var enm = enumerable.GetEnumerator();
            for (int i = 0; i <= index; i++)
            {
                if (!enm.MoveNext()) return null;
            }
            return enm.Current;
        }
        public static void SetIcon(string className, string explicitObjectName)
        {
            MonoScript script = GetAssetFromSearch<MonoScript>(string.Format("t:script {0}", className), className, className);
            MethodInfo setIconForObject = typeof(EditorGUIUtility).GetMethod("SetIconForObject", BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo copyMonoScriptIconToImporters = typeof(MonoImporter).GetMethod("CopyMonoScriptIconToImporters", BindingFlags.Static | BindingFlags.NonPublic);
            Texture2D icon = GetAssetFromSearch<Texture2D>("t:Texture2D " + explicitObjectName, "Texture2D", explicitObjectName);
            setIconForObject.Invoke(null, new object[]{ script, icon });
            copyMonoScriptIconToImporters.Invoke(null, new object[]{ script });
        }
        public static T GetAssetFromSearch<T> (string search, string explicitClassName, string explicitObjectName) where T : UnityEngine.Object
        {
            var results = AssetDatabase.FindAssets(search);
            T icon = null;
            if (results.Length > 0)
            {
                for (int i = 0; i < results.Length; i++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(results[i]);
                    var p = Path.GetFileNameWithoutExtension(path);
                    if (p == explicitObjectName)
                    {
                        icon = AssetDatabase.LoadAssetAtPath<T>(path);
                        if (icon != null)
                        {
                            return icon;
                        }
                    }
                }
            }
            return null;
        }
        public static object GetTargetObjectOfProperty(SerializedProperty prop)
        {
            if (prop == null) return null;

            string propPath = prop.propertyPath;
            object obj = prop.serializedObject.targetObject;
            object parentObj = obj;
            if (_targetObjectOfPropertyCache.ContainsKey(parentObj) &&
                _targetObjectOfPropertyCache[parentObj].ContainsKey(propPath))
            {
                return _targetObjectOfPropertyCache[parentObj][propPath];
            }
            
            var path = propPath.Replace(".Array.data[", "[");

            var elements = path.Split('.');
            foreach (var element in elements)
            {
                if (element.Contains("["))
                {
                    var elementName = element.Substring(0, element.IndexOf("["));
                    var index = System.Convert.ToInt32(element.Substring(element.IndexOf("[")).Replace("[", "").Replace("]", ""));
                    obj = GetValue_Imp(obj, elementName, index);
                }
                else
                {
                    obj = GetValue_Imp(obj, element);
                }
            }

            if (obj != null)
            {
                if (!_targetObjectOfPropertyCache.ContainsKey(parentObj))
                {
                    _targetObjectOfPropertyCache.Add(parentObj,
                        new Dictionary<string, object>());
                }
                if (!_targetObjectOfPropertyCache[parentObj].ContainsKey(propPath))
                {
                    _targetObjectOfPropertyCache[parentObj].Add(propPath, obj);
                }
            }
            return obj;
        }
    }
}