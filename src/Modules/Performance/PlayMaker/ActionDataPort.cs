using System;
using System.Collections.Generic;
using System.Reflection;
using HutongGames.PlayMaker;
using UnityEngine;

namespace WildWasteland.Modules.Performance
{
    /// <summary>
    /// FS4: exact C# copy of PlayMaker's private ActionData.LoadActionField (PlayMaker.dll, Sources
    /// code/PlayMaker/HutongGames.PlayMaker/ActionData.cs L807-1116), generated mechanically from the decompiled body:
    /// private Get*/LoadArrayElement calls go through open-instance delegates, private fields through generated getters,
    /// and only these two calls differ - FieldInfo.SetValue -> <see cref="FastActionLoadFeature.SetField"/>,
    /// Activator.CreateInstance -> <see cref="FastActionLoadFeature.Create"/> (both fall back to the reflection call
    /// whenever reflection would convert, default or throw, and are plain reflection while FS4 is off).
    /// Why a copy instead of the 0.1.26c transpiler: patching this method in place broke it in game ("Missing
    /// LoadActionField for type: System.Boolean", null fields). The prefix always runs this copy once installed, so the
    /// patched original body is never executed.
    /// </summary>
    internal static class ActionDataPort
    {
        static T M<T>(string name) where T : class
        {
            var mi = typeof(ActionData).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (mi == null) throw new MissingMethodException("ActionData", name);
            return (T)(object)Delegate.CreateDelegate(typeof(T), mi);
        }
        static readonly Func<ActionData, Fsm, int, FsmArray> _GetFsmArray = M<Func<ActionData, Fsm, int, FsmArray>>("GetFsmArray");
        static readonly Func<ActionData, Fsm, int, FsmBool> _GetFsmBool = M<Func<ActionData, Fsm, int, FsmBool>>("GetFsmBool");
        static readonly Func<ActionData, Fsm, int, FsmColor> _GetFsmColor = M<Func<ActionData, Fsm, int, FsmColor>>("GetFsmColor");
        static readonly Func<ActionData, Fsm, int, FsmEnum> _GetFsmEnum = M<Func<ActionData, Fsm, int, FsmEnum>>("GetFsmEnum");
        static readonly Func<ActionData, Fsm, int, FsmEventTarget> _GetFsmEventTarget = M<Func<ActionData, Fsm, int, FsmEventTarget>>("GetFsmEventTarget");
        static readonly Func<ActionData, Fsm, int, FsmFloat> _GetFsmFloat = M<Func<ActionData, Fsm, int, FsmFloat>>("GetFsmFloat");
        static readonly Func<ActionData, Fsm, int, FsmGameObject> _GetFsmGameObject = M<Func<ActionData, Fsm, int, FsmGameObject>>("GetFsmGameObject");
        static readonly Func<ActionData, Fsm, int, FsmInt> _GetFsmInt = M<Func<ActionData, Fsm, int, FsmInt>>("GetFsmInt");
        static readonly Func<ActionData, Fsm, int, FsmMaterial> _GetFsmMaterial = M<Func<ActionData, Fsm, int, FsmMaterial>>("GetFsmMaterial");
        static readonly Func<ActionData, Fsm, int, FsmObject> _GetFsmObject = M<Func<ActionData, Fsm, int, FsmObject>>("GetFsmObject");
        static readonly Func<ActionData, Fsm, int, FsmOwnerDefault> _GetFsmOwnerDefault = M<Func<ActionData, Fsm, int, FsmOwnerDefault>>("GetFsmOwnerDefault");
        static readonly Func<ActionData, Fsm, int, FsmProperty> _GetFsmProperty = M<Func<ActionData, Fsm, int, FsmProperty>>("GetFsmProperty");
        static readonly Func<ActionData, Fsm, int, FsmQuaternion> _GetFsmQuaternion = M<Func<ActionData, Fsm, int, FsmQuaternion>>("GetFsmQuaternion");
        static readonly Func<ActionData, Fsm, int, FsmRect> _GetFsmRect = M<Func<ActionData, Fsm, int, FsmRect>>("GetFsmRect");
        static readonly Func<ActionData, Fsm, int, FsmString> _GetFsmString = M<Func<ActionData, Fsm, int, FsmString>>("GetFsmString");
        static readonly Func<ActionData, Fsm, int, FsmTemplateControl> _GetFsmTemplateControl = M<Func<ActionData, Fsm, int, FsmTemplateControl>>("GetFsmTemplateControl");
        static readonly Func<ActionData, Fsm, int, FsmTexture> _GetFsmTexture = M<Func<ActionData, Fsm, int, FsmTexture>>("GetFsmTexture");
        static readonly Func<ActionData, Fsm, int, FsmVar> _GetFsmVar = M<Func<ActionData, Fsm, int, FsmVar>>("GetFsmVar");
        static readonly Func<ActionData, Fsm, int, FsmVector2> _GetFsmVector2 = M<Func<ActionData, Fsm, int, FsmVector2>>("GetFsmVector2");
        static readonly Func<ActionData, Fsm, int, FsmVector3> _GetFsmVector3 = M<Func<ActionData, Fsm, int, FsmVector3>>("GetFsmVector3");
        static readonly Func<ActionData, Fsm, int, FunctionCall> _GetFunctionCall = M<Func<ActionData, Fsm, int, FunctionCall>>("GetFunctionCall");
        static readonly Func<ActionData, Fsm, int, LayoutOption> _GetLayoutOption = M<Func<ActionData, Fsm, int, LayoutOption>>("GetLayoutOption");
        static readonly Func<ActionData, int, FsmAnimationCurve> _GetFsmAnimationCurve = M<Func<ActionData, int, FsmAnimationCurve>>("GetFsmAnimationCurve");
        static readonly Func<ActionData, int, string> _GetStringParam = M<Func<ActionData, int, string>>("GetStringParam");
        static readonly Action<ActionData, Fsm, Array, Type, int, int> _LoadArrayElement = M<Action<ActionData, Fsm, Array, Type, int, int>>("LoadArrayElement");
        static readonly Func<ActionData, byte[]> _byteDataAsArray = FastReflect.Getter<ActionData, byte[]>(typeof(ActionData), "byteDataAsArray");
        static readonly Func<ActionData, List<int>> _paramDataPos = FastReflect.Getter<ActionData, List<int>>(typeof(ActionData), "paramDataPos");
        static readonly Func<ActionData, List<int>> _paramByteDataSize = FastReflect.Getter<ActionData, List<int>>(typeof(ActionData), "paramByteDataSize");
        static readonly Func<ActionData, List<string>> _stringParams = FastReflect.Getter<ActionData, List<string>>(typeof(ActionData), "stringParams");
        static readonly Func<ActionData, List<UnityEngine.Object>> _unityObjectParams = FastReflect.Getter<ActionData, List<UnityEngine.Object>>(typeof(ActionData), "unityObjectParams");
        static readonly Func<ActionData, List<string>> _paramName = FastReflect.Getter<ActionData, List<string>>(typeof(ActionData), "paramName");
        static readonly Func<ActionData, List<int>> _customTypeSizes = FastReflect.Getter<ActionData, List<int>>(typeof(ActionData), "customTypeSizes");
        static readonly Func<ActionData, List<string>> _customTypeNames = FastReflect.Getter<ActionData, List<string>>(typeof(ActionData), "customTypeNames");
        static readonly Func<ActionData, List<int>> _arrayParamSizes = FastReflect.Getter<ActionData, List<int>>(typeof(ActionData), "arrayParamSizes");
        static readonly Func<ActionData, List<string>> _arrayParamTypes = FastReflect.Getter<ActionData, List<string>>(typeof(ActionData), "arrayParamTypes");
        static readonly Func<ActionData, int> _getNext = FastReflect.Getter<ActionData, int>(typeof(ActionData), "nextParamIndex");
        static readonly Action<ActionData, int> _setNext = FastReflect.Setter<ActionData, int>(typeof(ActionData), "nextParamIndex");

        /// <summary>Touches the static initializer (all delegates/getters resolve or throw here, before patching).</summary>
        internal static void Check() { if (_setNext == null || _LoadArrayElement == null) throw new Exception("ActionData members missing"); }

        static void Set(FieldInfo field, object obj, object value) { FastActionLoadFeature.SetField(field, obj, value); }
        static object New(Type t) { return FastActionLoadFeature.Create(t); }

        internal static void Load(ActionData self, Fsm fsm, object obj, FieldInfo field, int paramIndex)
        {
            Type fieldType = field.FieldType;
            object value = null;
            if ((object)fieldType == typeof(FsmGameObject))
            {
                value = _GetFsmGameObject(self, fsm, paramIndex);
            }
            else if ((object)fieldType == typeof(FsmEvent))
            {
                if (fsm.DataVersion > 1)
                {
                    string stringParam = _GetStringParam(self, paramIndex);
                    value = (string.IsNullOrEmpty(stringParam) ? null : FsmEvent.GetFsmEvent(stringParam));
                }
                else
                {
                    try
                    {
                        value = FsmUtility.ByteArrayToFsmEvent(_byteDataAsArray(self), _paramDataPos(self)[paramIndex], _paramByteDataSize(self)[paramIndex]);
                    }
                    catch
                    {
                        value = null;
                    }
                }
            }
            else if ((object)fieldType == typeof(FsmFloat))
            {
                value = _GetFsmFloat(self, fsm, paramIndex);
            }
            else if ((object)fieldType == typeof(FsmInt))
            {
                value = _GetFsmInt(self, fsm, paramIndex);
            }
            else if ((object)fieldType == typeof(FsmBool))
            {
                value = _GetFsmBool(self, fsm, paramIndex);
            }
            else if ((object)fieldType == typeof(FsmVector2))
            {
                value = _GetFsmVector2(self, fsm, paramIndex);
            }
            else if ((object)fieldType == typeof(FsmVector3))
            {
                value = _GetFsmVector3(self, fsm, paramIndex);
            }
            else if ((object)fieldType == typeof(FsmRect))
            {
                value = _GetFsmRect(self, fsm, paramIndex);
            }
            else if ((object)fieldType == typeof(FsmQuaternion))
            {
                value = _GetFsmQuaternion(self, fsm, paramIndex);
            }
            else if ((object)fieldType == typeof(FsmColor))
            {
                value = _GetFsmColor(self, fsm, paramIndex);
            }
            else if ((object)fieldType == typeof(FsmObject))
            {
                value = _GetFsmObject(self, fsm, paramIndex);
            }
            else if ((object)fieldType == typeof(FsmMaterial))
            {
                value = _GetFsmMaterial(self, fsm, paramIndex);
            }
            else if ((object)fieldType == typeof(FsmTexture))
            {
                value = _GetFsmTexture(self, fsm, paramIndex);
            }
            else if ((object)fieldType == typeof(FunctionCall))
            {
                value = _GetFunctionCall(self, fsm, paramIndex);
            }
            else if ((object)fieldType == typeof(FsmTemplateControl))
            {
                value = _GetFsmTemplateControl(self, fsm, paramIndex);
            }
            else if ((object)fieldType == typeof(FsmVar))
            {
                value = _GetFsmVar(self, fsm, paramIndex);
            }
            else if ((object)fieldType == typeof(FsmEnum))
            {
                value = _GetFsmEnum(self, fsm, paramIndex);
            }
            else if ((object)fieldType == typeof(FsmArray))
            {
                value = _GetFsmArray(self, fsm, paramIndex);
            }
            else if ((object)fieldType == typeof(FsmProperty))
            {
                value = _GetFsmProperty(self, fsm, paramIndex);
            }
            else if ((object)fieldType == typeof(FsmEventTarget))
            {
                value = _GetFsmEventTarget(self, fsm, paramIndex);
            }
            else if ((object)fieldType == typeof(LayoutOption))
            {
                value = _GetLayoutOption(self, fsm, paramIndex);
            }
            else if ((object)fieldType == typeof(FsmOwnerDefault))
            {
                value = _GetFsmOwnerDefault(self, fsm, paramIndex);
            }
            else if ((object)fieldType == typeof(FsmAnimationCurve))
            {
                value = _GetFsmAnimationCurve(self, paramIndex);
            }
            else if ((object)fieldType == typeof(FsmString))
            {
                value = _GetFsmString(self, fsm, paramIndex);
            }
            else if ((object)fieldType == typeof(float))
            {
                try
                {
                    value = FsmUtility.BitConverter.ToSingle(_byteDataAsArray(self), _paramDataPos(self)[paramIndex]);
                }
                catch
                {
                    value = 0f;
                }
            }
            else if ((object)fieldType == typeof(int))
            {
                try
                {
                    value = FsmUtility.BitConverter.ToInt32(_byteDataAsArray(self), _paramDataPos(self)[paramIndex]);
                }
                catch
                {
                    value = 0;
                }
            }
            else if ((object)fieldType == typeof(bool))
            {
                try
                {
                    value = FsmUtility.BitConverter.ToBoolean(_byteDataAsArray(self), _paramDataPos(self)[paramIndex]);
                }
                catch
                {
                    value = false;
                }
            }
            else if ((object)fieldType == typeof(Color))
            {
                try
                {
                    value = FsmUtility.ByteArrayToColor(_byteDataAsArray(self), _paramDataPos(self)[paramIndex]);
                }
                catch
                {
                    value = default(Color);
                }
            }
            else if ((object)fieldType == typeof(Vector2))
            {
                try
                {
                    value = FsmUtility.ByteArrayToVector2(_byteDataAsArray(self), _paramDataPos(self)[paramIndex]);
                }
                catch
                {
                    value = default(Vector2);
                }
            }
            else if ((object)fieldType == typeof(Vector3))
            {
                try
                {
                    value = FsmUtility.ByteArrayToVector3(_byteDataAsArray(self), _paramDataPos(self)[paramIndex]);
                }
                catch
                {
                    value = default(Vector3);
                }
            }
            else if ((object)fieldType == typeof(Vector4))
            {
                try
                {
                    value = FsmUtility.ByteArrayToVector4(_byteDataAsArray(self), _paramDataPos(self)[paramIndex]);
                }
                catch
                {
                    value = default(Vector4);
                }
            }
            else if ((object)fieldType == typeof(Rect))
            {
                try
                {
                    value = FsmUtility.ByteArrayToRect(_byteDataAsArray(self), _paramDataPos(self)[paramIndex]);
                }
                catch
                {
                    value = default(Rect);
                }
            }
            else if ((object)fieldType == typeof(string))
            {
                if (fsm.DataVersion > 1 && _stringParams(self) != null && _stringParams(self).Count > 0)
                {
                    value = _GetStringParam(self, paramIndex);
                }
                else
                {
                    try
                    {
                        value = FsmUtility.ByteArrayToString(_byteDataAsArray(self), _paramDataPos(self)[paramIndex], _paramByteDataSize(self)[paramIndex]);
                    }
                    catch
                    {
                        value = "";
                    }
                }
            }
            else if (fieldType.IsEnum)
            {
                try
                {
                    value = Enum.ToObject(fieldType, (object)FsmUtility.BitConverter.ToInt32(_byteDataAsArray(self), _paramDataPos(self)[paramIndex]));
                }
                catch
                {
                    value = New(fieldType);
                }
            }
            else if (typeof(FsmObject).IsAssignableFrom(fieldType))
            {
                Set(field, obj, _GetFsmObject(self, fsm, paramIndex));
            }
            else
            {
                if (!typeof(UnityEngine.Object).IsAssignableFrom(fieldType))
                {
                    if (fieldType.IsArray)
                    {
                        try
                        {
                            Type globalType = ReflectionUtils.GetGlobalType(_arrayParamTypes(self)[_paramDataPos(self)[paramIndex]]);
                            int num = _arrayParamSizes(self)[_paramDataPos(self)[paramIndex]];
                            Array array = Array.CreateInstance(globalType, num);
                            for (int i = 0; i < num; i++)
                            {
                                _setNext(self, _getNext(self) + 1);
                                _LoadArrayElement(self, fsm, array, globalType, i, _getNext(self));
                            }
                            Set(field, obj, array);
                            return;
                        }
                        catch
                        {
                            Set(field, obj, null);
                            return;
                        }
                    }
                    if (fieldType.IsClass)
                    {
                        try
                        {
                            Type globalType2 = ReflectionUtils.GetGlobalType(_customTypeNames(self)[_paramDataPos(self)[paramIndex]]);
                            object obj14 = New(globalType2);
                            int num2 = _customTypeSizes(self)[_paramDataPos(self)[paramIndex]];
                            for (int j = 0; j < num2; j++)
                            {
                                _setNext(self, _getNext(self) + 1);
                                FieldInfo field2 = globalType2.GetField(_paramName(self)[_getNext(self)]);
                                if ((object)field2 != null)
                                {
                                    Load(self, fsm, obj14, field2, _getNext(self));
                                }
                            }
                            Set(field, obj, obj14);
                            return;
                        }
                        catch
                        {
                            Set(field, obj, null);
                            return;
                        }
                    }
                    UnityEngine.Debug.LogError("ActionData: Missing LoadActionField for type: " + fieldType);
                    Set(field, obj, null);
                    return;
                }
                try
                {
                    UnityEngine.Object obj16 = _unityObjectParams(self)[_paramDataPos(self)[paramIndex]];
                    if ((object)obj16 != null)
                    {
                        if ((object)obj16.GetType() != typeof(UnityEngine.Object))
                        {
                            Set(field, obj, obj16);
                        }
                        return;
                    }
                }
                catch
                {
                    Set(field, obj, null);
                    return;
                }
            }
            Set(field, obj, value);
        }
    }
}
