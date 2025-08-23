using System;
using System.Diagnostics;
using UnityEngine;

namespace Game.Scripts
{
    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.Field)]
    public class InlinePropertyAttribute : PropertyAttribute
    {
        public InlinePropertyAttribute() { }
    }
}