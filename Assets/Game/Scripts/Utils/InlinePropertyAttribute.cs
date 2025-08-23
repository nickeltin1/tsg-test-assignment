using System;
using System.Diagnostics;
using UnityEngine;

namespace Game.Scripts
{
    /// <summary>
    /// Simple attribute to draw field inlined (no dropdown, just flat child properties)
    /// </summary>
    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.Field)]
    public class InlinePropertyAttribute : PropertyAttribute
    {
        public InlinePropertyAttribute() { }
    }
}